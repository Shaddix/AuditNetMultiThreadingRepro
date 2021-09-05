using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace AuditNetMultiThreadingRepro.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ProductController : ControllerBase
    {
        private readonly Func<MyDbContext> _dbContextFactory;


        public ProductController(Func<MyDbContext> dbContextFactory)
        {
            _dbContextFactory = dbContextFactory;
        }

        [HttpGet]
        public async Task Post()
        {
            await RetryInTransactionAsync(async (db) =>
            {
                var prc = await db.Products.CountAsync();
                var product = new Product();
                db.Products.Add(product);

                await db.SaveChangesAsync();
                await Task.Delay(3000);

                var product2 = new Product();
                db.Products.Add(product2);
                await db.SaveChangesAsync();
                return product2.Id;
            });
        }

        public async Task<TResult> RetryInTransactionAsync<TResult>(
            Func<MyDbContext, Task<TResult>> action)
        {
            TResult result = default;
            bool failed;
            int retries = 0;

            do
            {
                failed = false;
                try
                {
                    using (MyDbContext db = _dbContextFactory())
                    {
                        await using IDbContextTransaction transaction =
                            await db.BeginTransactionAsync();
                        result = await action(db);
                        await transaction.CommitAsync();
                    }
                }
                catch (Exception ex) when (retries < 5 &&
                                           TransientHelper.IsTransientPostgresError(ex))
                {
                    failed = true;
                    retries++;

                    // Delay grows exponentially: 5, 25, 125, ... 3125ms.
                    var sleepTime = TimeSpan.FromMilliseconds(Math.Pow(5.0, retries));
                    await Task.Delay(sleepTime);
                }
            } while (failed);

            return result;
        }
    }
}