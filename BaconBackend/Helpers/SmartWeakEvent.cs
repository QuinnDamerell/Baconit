// Copyright (c) 2008 Daniel Grunwald
// 
// Permission is hereby granted, free of charge, to any person
// obtaining a copy of this software and associated documentation
// files (the "Software"), to deal in the Software without
// restriction, including without limitation the rights to use,
// copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following
// conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
// OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
// HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
// WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
// OTHER DEALINGS IN THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace BaconBackend.Helpers
{
    /// <summary>
    /// A class for managing a weak event.
    /// </summary>
    public sealed class SmartWeakEvent<T> where T : class
    {
        private Func<EventArgs, bool> _mBetweenInvokesFunc;

        private struct EventEntry
        {
            public readonly MethodInfo TargetMethod;
            public readonly WeakReference TargetReference;

            public EventEntry(MethodInfo targetMethod, WeakReference targetReference)
            {
                TargetMethod = targetMethod;
                TargetReference = targetReference;
            }
        }

        private readonly List<EventEntry> _eventEntries = new List<EventEntry>();

        static SmartWeakEvent()
        {
            if (!typeof(T).GetTypeInfo().IsSubclassOf(typeof(Delegate)))
                throw new ArgumentException("T must be a delegate type");
            var invoke = typeof(T).GetTypeInfo().GetDeclaredMethod("Invoke");
            if (invoke == null || invoke.GetParameters().Length != 2)
                throw new ArgumentException("T must be a delegate type taking 2 parameters");
            var senderParameter = invoke.GetParameters()[0];
            if (senderParameter.ParameterType != typeof(object))
                throw new ArgumentException("The first delegate parameter must be of type 'object'");
            var argsParameter = invoke.GetParameters()[1];
            if (!(typeof(EventArgs).GetTypeInfo().IsAssignableFrom(argsParameter.ParameterType.GetTypeInfo())))
                throw new ArgumentException("The second delegate parameter must be derived from type 'EventArgs'");
            if (invoke.ReturnType != typeof(void))
                throw new ArgumentException("The delegate return type must be void.");
        }

        /// <summary>
        /// Sets a function to call between invokes of the callback. If the function returns true
        /// the callbacks will continue, if false they will stop.
        /// </summary>
        /// <param name="func"></param>
        public void SetInBetweenInvokesAction(Func<EventArgs, bool> func)
        {
            _mBetweenInvokesFunc = func;
        }

        public void Add(T eh)
        {
            if (eh == null) return;
            var d = (Delegate)(object)eh;

            var attributes = d.GetMethodInfo().DeclaringType.GetTypeInfo().GetCustomAttributes(typeof(CompilerGeneratedAttribute), false);
            var count = 0;
            using (var enumerator = attributes.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    count++;
                }
            }

            if (count != 0)
                throw new ArgumentException("Cannot create weak event to anonymous method with closure.");

            if (_eventEntries.Count == _eventEntries.Capacity)
                RemoveDeadEntries();
            var target = d.Target != null ? new WeakReference(d.Target) : null;
            _eventEntries.Add(new EventEntry(d.GetMethodInfo(), target));
        }

        private void RemoveDeadEntries()
        {
            _eventEntries.RemoveAll(ee => ee.TargetReference != null && !ee.TargetReference.IsAlive);
        }

        public void Remove(T eh)
        {
            if (eh == null) return;
            var d = (Delegate)(object)eh;
            for (var i = _eventEntries.Count - 1; i >= 0; i--)
            {
                var entry = _eventEntries[i];
                if (entry.TargetReference != null)
                {
                    var target = entry.TargetReference.Target;
                    if (target == null)
                    {
                        _eventEntries.RemoveAt(i);
                    }
                    else if (target == d.Target && entry.TargetMethod == d.GetMethodInfo())
                    {
                        _eventEntries.RemoveAt(i);
                        break;
                    }
                }
                else
                {
                    if (d.Target != null || entry.TargetMethod != d.GetMethodInfo()) continue;
                    _eventEntries.RemoveAt(i);
                    break;
                }
            }
        }

        public void Raise(object sender, EventArgs e)
        {
            var needsCleanup = false;
            object[] parameters = { sender, e };
            foreach (var ee in _eventEntries.ToArray())
            {
                if (ee.TargetReference != null)
                {
                    var target = ee.TargetReference.Target;
                    if (target != null)
                    {
                        ee.TargetMethod.Invoke(target, parameters);
                    }
                    else
                    {
                        needsCleanup = true;
                    }
                }
                else
                {
                    ee.TargetMethod.Invoke(null, parameters);
                }

                if (_mBetweenInvokesFunc == null) continue;
                // Call the function, if it returns false stop the callbacks.
                if(!_mBetweenInvokesFunc.Invoke(e))
                {
                    break;
                }
            }
            if (needsCleanup)
                RemoveDeadEntries();
        }
    }
}
