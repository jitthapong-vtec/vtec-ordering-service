using System;
using System.Threading.Tasks;
using VerticalTec.POS.Database;

namespace VerticalTec.POS.Test
{
    class Program
    {
        static void Main(string[] args)
        {
            IDatabase db = new MySqlDatabase("127.0.0.1", "major_para", "3308");
            Task.Run(async () =>
            {
                await GetKioskPageAsync(db);
            });
            Console.ReadLine();
        }

        private static async Task GetKioskPageAsync(IDatabase db)
        {
            try
            {
                using (var conn = await db.ConnectAsync())
                {
                    VtecRepo repo = new VtecRepo(db);
                    var kioskPage = await repo.GetKioskPageAsync(conn, 3);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
    }
}
