using Sisus.Init;
using Horcrux.Runtime.Abstractions.Persistence;

namespace Horcrux.Runtime.Implementations.Persistence
{
	/// <summary>
	/// Initializer for the <see cref="SaveBootstep"/> component.
	/// </summary>
	internal sealed class SaveBootstepInitializer : Initializer<SaveBootstep, BasePersistenceDataCollection>
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
			public BasePersistenceDataCollection SaveCollection = default;
		}
		#endif
	}
}
