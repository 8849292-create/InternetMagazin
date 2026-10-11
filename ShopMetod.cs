using System;
using System.Collections.Generic;
using System.Threading;

namespace InternetMagazin
{
    public class Product
    {
        public string Name { get; set; }
        public int PriceKopecks { get; set; } 
        public long Stock { get; set; } 

       
        public long InitialStock { get; set; }
        public long SuppliedCount { get; set; }
        public long SoldCount { get; set; }

        public Product(string name, int price, int initial)
        {
            Name = name;
            PriceKopecks = price;
            Stock = initial;
            InitialStock = initial;
        }
    }
    //-----------------------
    public class Warehouse
    {
        private readonly List<Product> products;
        private readonly object locker = new object();
        
        private static long totalRevenueKopecks;
        private static int completOrders;
        private static int cancelledOrders;

        public Warehouse()
        {
            products = new List<Product>
            {
                new Product("Ноутбук", 3500000, 20),
                new Product("Мышь", 15000, 20),
                new Product("Клавиатура", 45000, 20),
                new Product("Монитор", 2500000, 20),
                new Product("Наушники", 20000, 20)
            };
        }
        public bool TryPlaceOrder(List<(Product product, int qty)> orderItems, out long orderRevenue)
        {            
            orderRevenue = 0;
           
            bool canFulfill = true;
            foreach (var p in orderItems)
            {               
                if (p.product.Stock < p.qty)
                {
                    canFulfill = false; break;
                }
            }

            if (!canFulfill)
            {                
                lock (locker)
                {                    
                    foreach (var p in orderItems)
                    {
                        if (p.product.Stock < p.qty)
                        {                           
                            bool signaled = Monitor.Wait(locker, 2000);
                                                        
                            if (p.product.Stock < p.qty)
                            {                                
                                Interlocked.Increment(ref cancelledOrders);
                                return false;
                            }
                        }
                    }
                }
            }                       
            lock (locker)
            {                
                foreach (var it in orderItems)
                {
                    if (it.product.Stock < it.qty)
                    {
                        Interlocked.Increment(ref cancelledOrders);
                        return false;
                    }
                }
                                
                long revenue = 0;
                foreach (var it in orderItems)
                {
                    it.product.Stock -= it.qty;
                    it.product.SoldCount += it.qty;
                    revenue += (long)it.product.PriceKopecks * it.qty;
                }

                orderRevenue = revenue;
                Interlocked.Add(ref totalRevenueKopecks, revenue);
                Interlocked.Increment(ref completOrders);
                return true;
            }
        }
        // ---
        public void RestockRandom()
        {
            Random rdn = new Random();
            int productIndex = rdn.Next(products.Count);
            int amount = rdn.Next(10, 16); 

            lock (locker)
            {
                products[productIndex].Stock += amount;
                products[productIndex].SuppliedCount += amount;

                Monitor.PulseAll(locker);
            }
        }
        //---
        public void PrintReport()
        {
            lock (locker)
            {
                Console.WriteLine("\n--- АНАЛИТИКА ---");
                Console.WriteLine($"Выручка: {totalRevenueKopecks / 100:N0} грн. ({totalRevenueKopecks} коп.)");
                Console.WriteLine($"Выполнено заказов: {completOrders}");
                Console.WriteLine($"Отменено заказов: {cancelledOrders}");

                Console.WriteLine("Остатки товаров:");
                foreach (var p in products)
                {
                    Console.WriteLine($"  {p.Name}: {p.Stock} шт.");
                }
                Console.WriteLine("----------------------\n");
            }
        }
        public void PrintShema()
        {
            Console.WriteLine("------ СХЕМА ------");
            Console.WriteLine(@"
       [Warehouse.locker] 
            |
            |-- [Поток(8 шт): Customer] 
            |      ├─ Блокируется на locker при нехватке товара (Monitor.Wait)
            |      └─ Блокируется на locker для списания товаров (lock)
            |
            |-- [Поток: Supplier x1]
            |      |- Блокируется на locker для пополнения (lock)
            |      |-Будит всех (Monitor.PulseAll)
            |
            |-- [Поток: Analyst x1]
                    |-- Блокируется на locker для чтения данных (lock)");
        }
        public void CheckInvariant()
        {
            Console.WriteLine("=== ПРОВЕРКА ИНВАРИАНТА ===");
            bool allOk = true;
            foreach (var p in products)
            {
                long calculated = p.InitialStock + p.SuppliedCount - p.SoldCount;
                if (calculated != p.Stock)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"ERROR {p.Name}: Ожидается {calculated}, pеально {p.Stock}");
                    allOk = false;
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"Товар {p.Name}: {calculated} == {p.Stock}");
                }
                Console.ResetColor();
            }
            Console.WriteLine("==========================\n");
        }
        public List<Product> GetProducts()
        {
            return products;
        }          
            
    }
    public class Customer
    {
        private readonly Warehouse Warehouse;
        private readonly Random rdn;
        private readonly int Id;

        public Customer(int id, Warehouse warehouse)
        {
            Id = id;
            Warehouse = warehouse;
            rdn = new Random(id); 
        }

        public void Run()
        {
            while (true)
            {
                Thread.Sleep(300 + rdn.Next(200));

                var order = new List<(Product, int)>();
                var products = Warehouse.GetProducts();
                
                int count = rdn.Next(1, 4);
                var indices = new HashSet<int>();
                while (indices.Count < count)
                {
                    indices.Add(rdn.Next(products.Count));
                }

                foreach (var p in indices)
                {
                    int qty = rdn.Next(1, 4); 
                    order.Add((products[p], qty));
                }

                long revenue;
                bool success = Warehouse.TryPlaceOrder(order, out revenue);

                if (success)
                {
                    Console.WriteLine($"Заказ для покупателя {Id}: выполнен, выручка: {revenue/100} грн.");
                }
                else
                {
                    Console.WriteLine($"Заказ для покупателя {Id}: отменен (нет товара)");
                }
            }
        }
    }
    public class Supplier
    {
        private readonly Warehouse Warehouse;
        private readonly Random rdn;

        public Supplier(Warehouse warehouse)
        {
            Warehouse = warehouse;
            rdn = new Random();
        }
        public void Run()
        {
            while (true)
            {
                Thread.Sleep(700);
                Warehouse.RestockRandom();
                Console.WriteLine("  Склад пополнен");
            }
        }
    }
    public class Analyst
    {
        private readonly Warehouse Warehouse;
        public Analyst(Warehouse warehouse)
        {
            Warehouse = warehouse;
        }
        public void Run()
        {
            while (true)
            {
                Thread.Sleep(500);
                Warehouse.PrintReport();
            }
        }
    }
}


