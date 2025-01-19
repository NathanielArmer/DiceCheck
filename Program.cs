using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace DiceCheck
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("Monte Carlo Simulation: d6 vs d12x2");
            Console.WriteLine("Comparing odds of rolling 1 on d6 vs rolling at least one 1 on two d12s");
            Console.WriteLine("----------------------------------------");

            const int simulations = 10_000_000;
            const int batchSize = 100_000; // Process in batches for better performance
            int numberOfBatches = simulations / batchSize;

            var stopwatch = Stopwatch.StartNew();

            var d6Results = new ConcurrentBag<int>();
            var d12Results = new ConcurrentBag<int>();

            // Run simulations in parallel using Random.Shared
            Parallel.For(0, numberOfBatches, _ =>
            {
                int localD6Successes = 0;
                int localD12Successes = 0;

                for (int i = 0; i < batchSize; i++)
                {
                    // Check d6
                    if (Random.Shared.Next(1, 7) == 1)
                    {
                        localD6Successes++;
                    }

                    // Check d12 (rolled twice)
                    int firstD12 = Random.Shared.Next(1, 13);
                    int secondD12 = Random.Shared.Next(1, 13);
                    if (firstD12 == 1 || secondD12 == 1)
                    {
                        localD12Successes++;
                    }
                }

                d6Results.Add(localD6Successes);
                d12Results.Add(localD12Successes);
            });

            stopwatch.Stop();

            // Calculate total successes
            int d6Successes = 0;
            int d12Successes = 0;
            foreach (var result in d6Results) d6Successes += result;
            foreach (var result in d12Results) d12Successes += result;

            // Calculate and display probabilities
            double d6Probability = (double)d6Successes / simulations;
            double d12Probability = (double)d12Successes / simulations;

            Console.WriteLine($"\nResults after {simulations:N0} simulations (completed in {stopwatch.ElapsedMilliseconds:N0}ms):");
            Console.WriteLine($"d6 probability of rolling 1: {d6Probability:P3}");
            Console.WriteLine($"d12x2 probability of rolling at least one 1: {d12Probability:P3}");
            Console.WriteLine($"\nTheoretical probabilities:");
            Console.WriteLine($"d6: {1.0/6:P3}");
            Console.WriteLine($"d12x2: {1 - (11.0/12 * 11.0/12):P3}");

            var difference = Math.Abs(d6Probability - d12Probability);
            Console.WriteLine($"\nDifference in probabilities: {difference:P3}");

            if (d6Probability > d12Probability)
            {
                Console.WriteLine("Rolling a d6 has better odds of getting a 1!");
            }
            else if (d12Probability > d6Probability)
            {
                Console.WriteLine("Rolling two d12s has better odds of getting at least one 1!");
            }
            else
            {
                Console.WriteLine("The odds are exactly the same!");
            }
        }
    }
}
