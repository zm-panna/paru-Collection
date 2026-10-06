using Microsoft.Data.SqlClient;
namespace Ecommerce.Infrastructure;
// SQL Server error 1205 guarantees the victim transaction was rolled back.
// Retry the complete business transaction, never an individual statement.
public static class SqlRetry{
 public static async Task<T> RunAsync<T>(AppDb db,Func<Task<T>> action){for(var attempt=0;;attempt++){try{return await action();}catch(Exception ex) when(attempt<3&&Deadlock(ex)){db.ChangeTracker.Clear();await Task.Delay(40*(attempt+1)+Random.Shared.Next(20,60));}}}
 static bool Deadlock(Exception ex){for(Exception? current=ex;current!=null;current=current.InnerException)if(current is SqlException sql&&sql.Number==1205)return true;return false;}
}
