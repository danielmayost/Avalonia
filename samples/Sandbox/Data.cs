namespace Sandbox;

public class Data : List<double>
{
    public Data()
    {
        Random r = new Random();
        var data = Enumerable.Repeat(0, 10000).
            Select(x => r.NextDouble(0.3, 2.5));
        AddRange(data);
    }
}

public static class RandomExtensions
{
    public static double NextDouble(
        this Random random,
        double minValue,
        double maxValue)
    {
        return random.NextDouble() * (maxValue - minValue) + minValue;
    }
}