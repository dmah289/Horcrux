namespace Horcrux.Runtime.Abstractions.Bootstrap
{
    /// <summary>Progress of one init or reinit phase. A splash reads <see cref="Ratio01"/> and <see cref="StepName"/>.</summary>
    public readonly struct BootProgress
    {
        /// <summary>Index of the step ABOUT to run; equals <see cref="StepCount"/> on the closing pulse.</summary>
        public readonly int StepIndex;

        /// <summary>Total steps in this phase.</summary>
        public readonly int StepCount;

        /// <summary>GameObject name of the step.</summary>
        public readonly string StepName;
        
        public BootProgress(int stepIndex, int stepCount, string stepName)
        {
            StepIndex = stepIndex;
            StepCount = stepCount;
            StepName = stepName;
        }
        
        
        public float Ratio01 => StepCount <= 0 ? 1f : (float)StepIndex / StepCount;

        /// <summary>True on the closing pulse: every step of the phase has run.</summary>
        public bool IsFinished => StepIndex >= StepCount;
    }
}