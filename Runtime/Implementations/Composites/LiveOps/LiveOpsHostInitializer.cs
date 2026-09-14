using Sisus.Init;
using Horcrux.Runtime.Abstractions.Time;

namespace Horcrux.Runtime.Implementations.Composites.LiveOps
{
	/// <summary>
	/// Initializer for the <see cref="LiveOpsHost"/> component.
	/// </summary>
	internal sealed class LiveOpsHostInitializer : Initializer<LiveOpsHost, ITimeService>
	{
		#if UNITY_EDITOR
		/// <summary>
		/// This section can be used to customize how the Init arguments will be drawn in the Inspector.
		/// <para>
		/// The Init argument names shown in the Inspector will match the names of members defined inside this section.
		/// </para>
		/// <para>
		/// Any PropertyAttributes attached to these members will also affect the Init arguments in the Inspector.
		/// </para>
		/// </summary>
		private sealed class Init
		{
			public ITimeService TimeService = default;
		}
		#endif
	}
}
