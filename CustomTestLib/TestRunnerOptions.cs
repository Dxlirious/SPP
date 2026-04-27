namespace CustomTestLib
{
    public class TestRunnerOptions
    {
        public int MaxDegreeOfParallelism { get; set; } = 4;
        public bool RunInParallel { get; set; } = true;
        public bool WriteToConsole { get; set; } = true;
    }
}