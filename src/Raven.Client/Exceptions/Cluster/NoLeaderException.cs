﻿using System;

namespace Raven.Client.Exceptions.Cluster
{
    public sealed class NoLeaderException : RavenException
    {
        public NoLeaderException()
        {
        }

        public NoLeaderException(string message) : base(message)
        {
        }

        public NoLeaderException(string message, Exception inner) : base(message, inner)
        {
        }
    }
}
