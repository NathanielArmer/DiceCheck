using DiceCheck.Models;

namespace DiceCheck;

public class Program
{
    private const int DefaultSimulations = 1_000_000;

    public static void Main(string[] args)
    {
        Console.WriteLine("DiceCheck - Monte Carlo Dice Roll Simulator");

        var (roll1, condition1, roll2, condition2, simulations) = args.Length switch
        {
            0 => GetInteractiveInput(),
            >= 8 => ParseCommandLineArgs(args),
            _ => throw new ArgumentException("Invalid number of arguments. Use no arguments for interactive mode, or provide: dice1 sides1 condition1 value1 [count1] dice2 sides2 condition2 value2 [count2] [simulations]")
        };

        var startTime = DateTime.Now;
        var (prob1, prob2) = SimulateScenarios(roll1, condition1, roll2, condition2, simulations);
        var duration = DateTime.Now - startTime;

        Console.WriteLine($"\nResults after {simulations:N0} simulations (completed in {duration.TotalMilliseconds:N0}ms):");
        Console.WriteLine($"Scenario 1: {roll1.Count}d{roll1.Sides} with condition {condition1}");
        Console.WriteLine($"Probability: {prob1:P3}");
        Console.WriteLine($"Scenario 2: {roll2.Count}d{roll2.Sides} with condition {condition2}");
        Console.WriteLine($"Probability: {prob2:P3}\n");
        Console.WriteLine($"Difference in probabilities: {Math.Abs(prob1 - prob2):P3}");
        Console.WriteLine(prob1 > prob2 ? "Scenario 1 is more likely!" : prob1 < prob2 ? "Scenario 2 is more likely!" : "Both scenarios are equally likely!");
    }

    private static (double prob1, double prob2) SimulateScenarios(DiceRoll roll1, RollCondition condition1, DiceRoll roll2, RollCondition condition2, int simulations)
    {
        const int BatchSize = 10_000;
        int numberOfBatches = simulations / BatchSize;
        var remainingSimulations = simulations % BatchSize;

        // Use thread-safe counters for parallel processing
        int successes1 = 0;
        int successes2 = 0;

        // Process batches in parallel
        Parallel.For(0, numberOfBatches, _ =>
        {
            int batchSuccesses1 = 0;
            int batchSuccesses2 = 0;

            for (int i = 0; i < BatchSize; i++)
            {
                if (condition1.Evaluate(roll1.Roll())) batchSuccesses1++;
                if (condition2.Evaluate(roll2.Roll())) batchSuccesses2++;
            }

            Interlocked.Add(ref successes1, batchSuccesses1);
            Interlocked.Add(ref successes2, batchSuccesses2);
        });

        // Process remaining simulations
        if (remainingSimulations > 0)
        {
            for (int i = 0; i < remainingSimulations; i++)
            {
                if (condition1.Evaluate(roll1.Roll())) successes1++;
                if (condition2.Evaluate(roll2.Roll())) successes2++;
            }
        }

        return ((double)successes1 / simulations, (double)successes2 / simulations);
    }

    private static (DiceRoll, RollCondition, DiceRoll, RollCondition, int) GetInteractiveInput()
    {
        Console.WriteLine("\nScenario 1:");
        var (roll1, condition1) = GetScenarioInput();

        Console.WriteLine("\nScenario 2:");
        var (roll2, condition2) = GetScenarioInput();

        Console.Write("\nNumber of simulations (default: 1,000,000): ");
        var input = Console.ReadLine();
        int simulations = string.IsNullOrWhiteSpace(input) ? DefaultSimulations : int.Parse(input);

        return (roll1, condition1, roll2, condition2, simulations);
    }

    private static (DiceRoll, RollCondition) GetScenarioInput()
    {
        Console.Write("Number of dice: ");
        int count = int.Parse(Console.ReadLine() ?? "1");

        Console.Write("Number of sides per die: ");
        int sides = int.Parse(Console.ReadLine() ?? "6");

        Console.WriteLine("\nCondition types:");
        foreach (var type in Enum.GetValues<ConditionType>())
        {
            Console.WriteLine($"  {(int)type}. {type}");
        }
        Console.Write("Select condition type (number): ");
        var conditionType = (ConditionType)int.Parse(Console.ReadLine() ?? "0");

        if (conditionType == ConditionType.CountMatching)
        {
            Console.Write("How many matching dice? ");
            int matchCount = int.Parse(Console.ReadLine() ?? "1");
            Console.Write("What value to match? ");
            int matchValue = int.Parse(Console.ReadLine() ?? "1");
            return (new DiceRoll(sides, count), RollCondition.CreateCountMatching(matchCount, matchValue));
        }
        else
        {
            Console.Write("Condition value: ");
            int value = int.Parse(Console.ReadLine() ?? "1");
            return (new DiceRoll(sides, count), RollCondition.Create(conditionType, value));
        }
    }

    private static (DiceRoll, RollCondition, DiceRoll, RollCondition, int) ParseCommandLineArgs(string[] args)
    {
        // Format: dice1 sides1 condition1 value1 [count1] dice2 sides2 condition2 value2 [count2] [simulations]
        var roll1 = new DiceRoll(int.Parse(args[1]), int.Parse(args[0]));
        var condition1 = (ConditionType)int.Parse(args[2]) == ConditionType.CountMatching
            ? RollCondition.CreateCountMatching(int.Parse(args[4]), int.Parse(args[3]))
            : RollCondition.Create((ConditionType)int.Parse(args[2]), int.Parse(args[3]));

        int offset = (ConditionType)int.Parse(args[2]) == ConditionType.CountMatching ? 5 : 4;
        
        var roll2 = new DiceRoll(int.Parse(args[offset + 1]), int.Parse(args[offset]));
        var condition2 = (ConditionType)int.Parse(args[offset + 2]) == ConditionType.CountMatching
            ? RollCondition.CreateCountMatching(int.Parse(args[offset + 4]), int.Parse(args[offset + 3]))
            : RollCondition.Create((ConditionType)int.Parse(args[offset + 2]), int.Parse(args[offset + 3]));

        offset = offset + ((ConditionType)int.Parse(args[offset + 2]) == ConditionType.CountMatching ? 5 : 4);
        int simulations = args.Length > offset ? int.Parse(args[offset]) : DefaultSimulations;

        return (roll1, condition1, roll2, condition2, simulations);
    }
}
