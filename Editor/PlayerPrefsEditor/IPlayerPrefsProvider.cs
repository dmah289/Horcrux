using System.Collections.Generic;

namespace Horcrux.Editor.PlayerPrefsEditor
{
    public interface IPlayerPrefsProvider
    {
        List<PlayerPrefsPair> PlayerPrefsPairs { get; }
        void MarkDirty();
    }
}
