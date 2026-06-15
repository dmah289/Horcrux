using System.Collections.Generic;

namespace Horcrux.Editor.PlayerPrefsEditor
{
    public interface IPlayerPrefsProvider
    {
        public List<PlayerPrefsPair> PlayerPrefsPairs { get; }
    }
}