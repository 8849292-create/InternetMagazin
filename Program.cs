using System;
using System.Text;
using System.Threading;

namespace InternetMagazin
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.WriteLine("===== Интернет-магазин =====\n");

            var warehouse = new Warehouse();

            Thread[] customers = new Thread[8];
            for (int i = 0; i < 8; i++)
            {
                customers[i] = new Thread(new Customer(i, warehouse).Run);
                customers[i].Start();
            }

            Thread supplier = new Thread(new Supplier(warehouse).Run);
            supplier.Start();

            Thread analyst = new Thread(new Analyst(warehouse).Run);
            analyst.Start();
            
            Thread.Sleep(10000);

            Console.WriteLine("\n=== ОСТАНОВКА РАБОТЫ ===");
            warehouse.CheckInvariant();
            warehouse.PrintShema();            
            Environment.Exit(0);

        }
    }
}
