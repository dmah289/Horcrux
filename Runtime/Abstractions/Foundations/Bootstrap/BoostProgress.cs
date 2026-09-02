namespace Horcrux.Runtime.Implementations.Bootstrap
{
    public readonly struct BoostProgress
    {
        public readonly int StepIdx;
        // total step count per round
        public readonly int StepCount;
        public readonly string StepName;
        
        public BoostProgress(int stepIdx, int stepCount, string stepName)
        {
            StepIdx = stepIdx;
            StepCount = stepCount;
            StepName = stepName;
        }
        
        public float Progress => StepCount <= 0 ? 1f : (float)StepIdx / StepCount;

        public bool IsFinished => StepIdx >= StepCount;
    }
}