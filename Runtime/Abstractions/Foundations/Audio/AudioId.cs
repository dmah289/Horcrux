using System;

namespace Horcrux.Runtime.Abstractions.Audio
{
    public readonly struct AudioId : IEquatable<AudioId>
    {
        public readonly int Value;
        
        public AudioId(int value) => Value = value;
        
        public static implicit operator AudioId(int value) => new(value);
        
        public bool Equals(AudioId other) => Value == other.Value;
        public override bool Equals(object obj) =>  obj is AudioId other && Equals(other);
        
        public bool IsValid => Value != 0;
        public override int GetHashCode() => Value;
        public override string ToString() =>  Value.ToString();
    }
}