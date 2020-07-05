using System;
using System.Collections.Generic;
using System.Text;

namespace RepoImageMan
{

    [Serializable]
    public class PackageCorruptException : Exception
    {
        public PackageCorruptException(string message) : base(message) { }
        public PackageCorruptException(string message, Exception inner) : base(message, inner) { }
        protected PackageCorruptException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context) : base(info, context) { }

        public PackageCorruptException() { }
    }
}
