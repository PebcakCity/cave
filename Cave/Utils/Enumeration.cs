using System;
using System.Reflection;

namespace Cave.Utils
{

    public abstract class Enumeration
    {
        public string Name {get; protected set;}
        public int Id {get; protected set;}
        protected Enumeration(int id, string name) => (Id, Name) = (id, name);
        protected Enumeration(Enumeration other) => (Id, Name) = (other.Id, other.Name);


        public override string ToString() => Name;

        /// <summary>
        /// Gets all public static fields (enum "members") of the Enumeration
        /// </summary>
        public static IEnumerable<T> GetAll<T>() where T : Enumeration =>
            typeof(T).GetFields( BindingFlags.Public |
                                BindingFlags.Static |
                                BindingFlags.DeclaredOnly )
                    .Select(field => field.GetValue(null))
                    .Cast<T>();

        /// <summary>
        /// Determines equality with another Enumeration member by whether their
        /// types and values both match
        /// </summary>
        public override bool Equals(object? obj)
        {
            if( obj is not Enumeration other )
                return false;
            var typeMatches = GetType().Equals(other.GetType());
            var valueMatches = Id.Equals(other.Id);
            return typeMatches && valueMatches;
        }

        public override int GetHashCode() => Id.GetHashCode();
        
        /// <summary>
        /// Returns the first member of the Enumeration matching the given predicate,
        /// or throws a KeyNotFoundException if none is found.
        /// </summary>
        private static T Parse<T, K>( K key, string valueOrName,
            Func<T, bool> predicate ) where T : Enumeration
        {
            var match = GetAll<T>().FirstOrDefault( predicate ) 
                ?? throw new KeyNotFoundException($"There is no member of type '{typeof(T)}' matching the {valueOrName} '{key}'.");
            return match;
        }

        /// <summary>
        /// Retrieves the first Enumeration member matching the given value if it exists.
        /// If it does not, a KeyNotFoundException will be thrown.
        /// </summary>
        public static T FromValue<T>( int value ) where T : Enumeration
        {
            try
            {
                var match = Parse<T, int>( value, "value", member => member.Id == value );
                return match;
            }
            catch ( KeyNotFoundException )
            {
                throw;
            }
        }

        /// <summary>
        /// Retrieves the first Enumeration member matching the given name if it exists.
        /// If it does not, a KeyNotFoundException will be thrown.
        /// </summary>
        public static T FromName<T>( string name ) where T : Enumeration
        {
            try
            {
                var match = Parse<T, string>( name, "name", member => member.Name == name );
                return match;
            }
            catch( KeyNotFoundException )
            {
                throw;
            }
        }
    }
}
