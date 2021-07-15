// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

//#define TRACE_HISTORY // this is used for advanced debugging when you need to trace where a packet is rented and returned, mostly used to identify double
                      // return problems

//#define TRACE_PATH  // this is used for advanced debugging when you need to see what functions the packet passes through. In each location you want to trace
                    // add a call to PushPath or PushPathStack e.g. packet.PushPath(new StackTrace().ToString()); and then when you hit a breakpoint or
                    // assertion failure inspect the _path variable to see the pushed entries since the packet was rented. 

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Microsoft.Data.SqlClient.SNI
{
#if DEBUG
    internal sealed partial class SNIPacket
    {
#if TRACE_HISTORY
        [DebuggerDisplay("{Action.ToString(),nq}")]
        internal struct History
        {
            public enum Direction
            {
                Rent = 0,
                Return = 1,
            }

            public Direction Action;
            public int RefCount;
            public string Stack;
        }
#endif

#if TRACE_PATH
        [DebuggerTypeProxy(typeof(PathEntryDebugView))]
        [DebuggerDisplay("{Name,nq}")]
        internal sealed class PathEntry
        {
            public PathEntry Previous = null;
            public string Name = null;
        }

        internal sealed class PathEntryDebugView
        {
            private readonly PathEntry _data;

            public PathEntryDebugView(PathEntry data)
            {
                if (data == null)
                {
                    throw new ArgumentNullException(nameof(data));
                }

                _data = data;
            }

            [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
            public string[] Items
            {
                get
                {
                    string[] items = Array.Empty<string>();
                    if (_data != null)
                    {
                        int count = 0;
                        for (PathEntry current = _data; current != null; current = current?.Previous)
                        {
                            count++;
                        }
                        items = new string[count];
                        int index = 0;
                        for (PathEntry current = _data; current != null; current = current?.Previous, index++)
                        {
                            items[index] = current.Name;
                        }
                    }
                    return items;
                }
            }
        }
#endif

        internal readonly int _id;  // in debug mode every packet is assigned a unique id so that the entire lifetime can be tracked when debugging
        /// refcount = 0 means that a packet should only exist in the pool
        /// refcount = 1 means that a packet is active
        /// refcount > 1 means that a packet has been reused in some way and is a serious error
        internal int _refCount;
        internal readonly SNIHandle _owner; // used in debug builds to check that packets are being returned to the correct pool
        internal string _traceTag; // used to assist tracing what steps the packet has been through
#if TRACE_PATH
        internal PathEntry _path;
#endif
#if TRACE_HISTORY
        internal List<History> _history;
#endif

        public void PushPath(string name)
        {
#if TRACE_PATH
            var entry = new PathEntry { Previous = _path, Name = name };
            _path = entry;
#endif
        }

        public void PushPathStack()
        {
#if TRACE_PATH
            PushPath(new StackTrace().ToString());
#endif
        }

        public void PopPath()
        {
#if TRACE_PATH
            _path = _path?.Previous;
#endif
        }

        public void ClearPath()
        {
#if TRACE_PATH
            _path = null;
#endif
        }

        public void AddHistory(bool renting)
        {
#if TRACE_HISTORY
            _history.Add(
                new History 
                {
                    Action = renting ? History.Direction.Rent : History.Direction.Return,
                    Stack = GetStackParts(), 
                    RefCount = _refCount 
                }
            );
#endif
        }

        /// <summary>
        /// uses the packet refcount in debug mode to identify if the packet is considered active
        /// it is an error to use a packet which is not active in any function outside the pool implementation
        /// </summary>
        public bool IsActive => _refCount == 1;

        public SNIPacket(SNIHandle owner, int id)
            : this()
        {
            _id = id;
            _owner = owner;
#if TRACE_PATH
            _path = null;
#endif
#if TRACE_HISTORY
            _history = new List<History>();
#endif
        }

        // the finalizer is only included in debug builds and is used to ensure that all packets are correctly recycled
        // it is not an error if a packet is dropped but it is undesirable so all efforts should be made to make sure we
        // do not drop them for the GC to pick up
        ~SNIPacket()
        {
            if (_data != null)
            {
                Debug.Fail($@"finalizer called for unreleased SNIPacket, tag: {_traceTag}");
            }
        }

#if TRACE_HISTORY
        private string GetStackParts()
        {
            return string.Join(Environment.NewLine,
                Environment.StackTrace
                .Split(new string[] { Environment.NewLine }, StringSplitOptions.None)
                .Skip(3) // trims off the common parts at the top of the stack so you can see what the actual caller was
                .Take(9) // trims off most of the bottom of the stack because when running under xunit there's a lot of spam
            );
        }
#endif
    }
#endif
}
