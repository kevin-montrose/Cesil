using Cesil.Tests;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Dynamic;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Cesil.Tests
{
    /* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the  Apache License, Version 2.0, please send an email to 
 * dlr@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 *
 * ***************************************************************************/

    /// <summary>
    /// Represents a dynamically assigned class.  Expando objects which share the same 
    /// members will share the same class.  Classes are dynamically assigned as the
    /// expando object gains members.
    /// </summary>
    internal class FakeExpandoClass
    {
        private readonly string[] _keys;                            // list of names associated with each element in the data array, sorted
        private readonly int _hashCode;                             // pre-calculated hash code of all the keys the class contains
        private Dictionary<int, List<WeakReference>> _transitions;  // cached transitions

        private const int EmptyHashCode = 6551;                     // hash code of the empty ExpandoClass.

        internal static FakeExpandoClass Empty = new FakeExpandoClass();    // The empty Expando class - all Expando objects start off w/ this class.

        /// <summary>
        /// Constructs the empty ExpandoClass.  This is the class used when an
        /// empty Expando object is initially constructed.
        /// </summary>
        internal FakeExpandoClass()
        {
            _hashCode = EmptyHashCode;
            _keys = new string[0];
        }

        /// <summary>
        /// Constructs a new ExpandoClass that can hold onto the specified keys.  The
        /// keys must be sorted ordinally.  The hash code must be precalculated for 
        /// the keys.
        /// </summary>
        internal FakeExpandoClass(string[] keys, int hashCode)
        {
            _hashCode = hashCode;
            _keys = keys;
        }

        /// <summary>
        /// Finds or creates a new ExpandoClass given the existing set of keys
        /// in this ExpandoClass plus the new key to be added. Members in an
        /// ExpandoClass are always stored case sensitively.
        /// </summary>
        internal FakeExpandoClass FindNewClass(string newKey)
        {
            // just XOR the newKey hash code 
            int hashCode = _hashCode ^ newKey.GetHashCode();

            lock (this)
            {
                List<WeakReference> infos = GetTransitionList(hashCode);

                for (int i = 0; i < infos.Count; i++)
                {
                    FakeExpandoClass klass = infos[i].Target as FakeExpandoClass;
                    if (klass == null)
                    {
                        infos.RemoveAt(i);
                        i--;
                        continue;
                    }

                    if (string.Equals(klass._keys[klass._keys.Length - 1], newKey, StringComparison.Ordinal))
                    {
                        // the new key is the key we added in this transition
                        return klass;
                    }
                }

                // no applicable transition, create a new one
                string[] keys = new string[_keys.Length + 1];
                Array.Copy(_keys, keys, _keys.Length);
                keys[_keys.Length] = newKey;
                FakeExpandoClass ec = new FakeExpandoClass(keys, hashCode);

                infos.Add(new WeakReference(ec));
                return ec;
            }
        }

        /// <summary>
        /// Gets the lists of transitions that are valid from this ExpandoClass
        /// to an ExpandoClass whos keys hash to the apporopriate hash code.
        /// </summary>
        private List<WeakReference> GetTransitionList(int hashCode)
        {
            if (_transitions == null)
            {
                _transitions = new Dictionary<int, List<WeakReference>>();
            }

            List<WeakReference> infos;
            if (!_transitions.TryGetValue(hashCode, out infos))
            {
                _transitions[hashCode] = infos = new List<WeakReference>();
            }

            return infos;
        }

        /// <summary>
        /// Gets the index at which the value should be stored for the specified name.
        /// </summary>
        internal int GetValueIndex(string name, bool caseInsensitive, FakeExpandoObject obj)
        {
            if (caseInsensitive)
            {
                return GetValueIndexCaseInsensitive(name, obj);
            }
            else
            {
                return GetValueIndexCaseSensitive(name);
            }
        }

        /// <summary>
        /// Gets the index at which the value should be stored for the specified name
        /// case sensitively. Returns the index even if the member is marked as deleted.
        /// </summary>
        internal int GetValueIndexCaseSensitive(string name)
        {
            for (int i = 0; i < _keys.Length; i++)
            {
                if (string.Equals(
                    _keys[i],
                    name,
                    StringComparison.Ordinal))
                {
                    return i;
                }
            }
            return FakeExpandoObject.NoMatch;
        }

        /// <summary>
        /// Gets the index at which the value should be stored for the specified name,
        /// the method is only used in the case-insensitive case.
        /// </summary>
        /// <param name="name">the name of the member</param>
        /// <param name="obj">The ExpandoObject associated with the class
        /// that is used to check if a member has been deleted.</param>
        /// <returns>
        /// the exact match if there is one
        /// if there is exactly one member with case insensitive match, return it
        /// otherwise we throw AmbiguousMatchException.
        /// </returns>
        private int GetValueIndexCaseInsensitive(string name, FakeExpandoObject obj)
        {
            int caseInsensitiveMatch = FakeExpandoObject.NoMatch; //the location of the case-insensitive matching member
            lock (obj.LockObject)
            {
                for (int i = _keys.Length - 1; i >= 0; i--)
                {
                    if (string.Equals(
                        _keys[i],
                        name,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        //if the matching member is deleted, continue searching
                        if (!obj.IsDeletedMember(i))
                        {
                            if (caseInsensitiveMatch == FakeExpandoObject.NoMatch)
                            {
                                caseInsensitiveMatch = i;
                            }
                            else
                            {
                                //Ambigous match, stop searching
                                return FakeExpandoObject.AmbiguousMatchFound;
                            }
                        }
                    }
                }
            }
            //There is exactly one member with case insensitive match.
            return caseInsensitiveMatch;
        }

        /// <summary>
        /// Gets the names of the keys that can be stored in the Expando class.  The
        /// list is sorted ordinally.
        /// </summary>
        internal string[] Keys
        {
            get
            {
                return _keys;
            }
        }
    }


    /// <summary>
    /// Represents an object with members that can be dynamically added and removed at runtime.
    /// </summary>
    public sealed class FakeExpandoObject : IDynamicMetaObjectProvider, IDictionary<string, object>, INotifyPropertyChanged
    {
        internal readonly object LockObject;                          // the readonly field is used for locking the Expando object
        private FakeExpandoData _data;                                    // the data currently being held by the Expando object
        private int _count;                                           // the count of available members

        internal static readonly object Uninitialized = new object(); // A marker object used to identify that a value is uninitialized.

        internal const int AmbiguousMatchFound = -2;        // The value is used to indicate there exists ambiguous match in the Expando object
        internal const int NoMatch = -1;                    // The value is used to indicate there is no matching member

        private PropertyChangedEventHandler _propertyChanged;

        /// <summary>
        /// Creates a new ExpandoObject with no members.
        /// </summary>
        public FakeExpandoObject()
        {
            _data = FakeExpandoData.Empty;
            LockObject = new object();
        }

        #region Get/Set/Delete Helpers

        /// <summary>
        /// Try to get the data stored for the specified class at the specified index.  If the
        /// class has changed a full lookup for the slot will be performed and the correct
        /// value will be retrieved.
        /// </summary>
        internal bool TryGetValue(object indexClass, int index, string name, bool ignoreCase, out object value)
        {
            // read the data now.  The data is immutable so we get a consistent view.
            // If there's a concurrent writer they will replace data and it just appears
            // that we won the ----
            FakeExpandoData data = _data;
            if (data.Class != indexClass || ignoreCase)
            {
                /* Re-search for the index matching the name here if
                 *  1) the class has changed, we need to get the correct index and return
                 *  the value there.
                 *  2) the search is case insensitive:
                 *      a. the member specified by index may be deleted, but there might be other
                 *      members matching the name if the binder is case insensitive.
                 *      b. the member that exactly matches the name didn't exist before and exists now,
                 *      need to find the exact match.
                 */
                index = data.Class.GetValueIndex(name, ignoreCase, this);
                if (index == FakeExpandoObject.AmbiguousMatchFound)
                {
                    throw new Exception();
                }
            }

            if (index == FakeExpandoObject.NoMatch)
            {
                value = null;
                return false;
            }

            // Capture the value into a temp, so it doesn't get mutated after we check
            // for Uninitialized.
            object temp = data[index];
            if (temp == Uninitialized)
            {
                value = null;
                return false;
            }

            // index is now known to be correct
            value = temp;
            return true;
        }

        /// <summary>
        /// Sets the data for the specified class at the specified index.  If the class has
        /// changed then a full look for the slot will be performed.  If the new class does
        /// not have the provided slot then the Expando's class will change. Only case sensitive
        /// setter is supported in ExpandoObject.
        /// </summary>
        internal void TrySetValue(object indexClass, int index, object value, string name, bool ignoreCase, bool add)
        {
            FakeExpandoData data;
            object oldValue;

            lock (LockObject)
            {
                data = _data;

                if (data.Class != indexClass || ignoreCase)
                {
                    // The class has changed or we are doing a case-insensitive search, 
                    // we need to get the correct index and set the value there.  If we 
                    // don't have the value then we need to promote the class - that 
                    // should only happen when we have multiple concurrent writers.
                    index = data.Class.GetValueIndex(name, ignoreCase, this);
                    if (index == FakeExpandoObject.AmbiguousMatchFound)
                    {
                        throw new Exception();
                    }
                    if (index == FakeExpandoObject.NoMatch)
                    {
                        // Before creating a new class with the new member, need to check 
                        // if there is the exact same member but is deleted. We should reuse
                        // the class if there is such a member.
                        int exactMatch = ignoreCase ?
                            data.Class.GetValueIndexCaseSensitive(name) :
                            index;
                        if (exactMatch != FakeExpandoObject.NoMatch)
                        {
                            Debug.Assert(data[exactMatch] == Uninitialized);
                            index = exactMatch;
                        }
                        else
                        {
                            FakeExpandoClass newClass = data.Class.FindNewClass(name);
                            data = PromoteClassCore(data.Class, newClass);
                            // After the class promotion, there must be an exact match,
                            // so we can do case-sensitive search here.
                            index = data.Class.GetValueIndexCaseSensitive(name);
                            Debug.Assert(index != FakeExpandoObject.NoMatch);
                        }
                    }
                }

                // Setting an uninitialized member increases the count of available members
                oldValue = data[index];
                if (oldValue == Uninitialized)
                {
                    _count++;
                }
                else if (add)
                {
                    throw new Exception();
                }

                data[index] = value;
            }

            // Notify property changed, outside of the lock.
            var propertyChanged = _propertyChanged;
            if (propertyChanged != null && value != oldValue)
            {
                // Use the canonical case for the key.
                propertyChanged(this, new PropertyChangedEventArgs(data.Class.Keys[index]));
            }
        }

        /// <summary>
        /// Deletes the data stored for the specified class at the specified index.
        /// </summary>
        internal bool TryDeleteValue(object indexClass, int index, string name, bool ignoreCase, object deleteValue)
        {
            FakeExpandoData data;
            lock (LockObject)
            {
                data = _data;

                if (data.Class != indexClass || ignoreCase)
                {
                    // the class has changed or we are doing a case-insensitive search,
                    // we need to get the correct index.  If there is no associated index
                    // we simply can't have the value and we return false.
                    index = data.Class.GetValueIndex(name, ignoreCase, this);
                    if (index == FakeExpandoObject.AmbiguousMatchFound)
                    {
                        throw new Exception();
                    }
                }
                if (index == FakeExpandoObject.NoMatch)
                {
                    return false;
                }

                object oldValue = data[index];
                if (oldValue == Uninitialized)
                {
                    return false;
                }

                // Make sure the value matches, if requested.
                //
                // It's a shame we have to call Equals with the lock held but
                // there doesn't seem to be a good way around that, and
                // ConcurrentDictionary in mscorlib does the same thing.
                if (deleteValue != Uninitialized && !object.Equals(oldValue, deleteValue))
                {
                    return false;
                }

                data[index] = Uninitialized;

                // Deleting an available member decreases the count of available members
                _count--;
            }

            // Notify property changed, outside of the lock.
            var propertyChanged = _propertyChanged;
            if (propertyChanged != null)
            {
                // Use the canonical case for the key.
                propertyChanged(this, new PropertyChangedEventArgs(data.Class.Keys[index]));
            }

            return true;
        }

        /// <summary>
        /// Returns true if the member at the specified index has been deleted,
        /// otherwise false. Call this function holding the lock.
        /// </summary>
        internal bool IsDeletedMember(int index)
        {
            Debug.Assert(index >= 0 && index <= _data.Length);

            if (index == _data.Length)
            {
                // The member is a newly added by SetMemberBinder and not in data yet
                return false;
            }

            return _data[index] == FakeExpandoObject.Uninitialized;
        }

        /// <summary>
        /// Exposes the ExpandoClass which we've associated with this 
        /// Expando object.  Used for type checks in rules.
        /// </summary>
        internal FakeExpandoClass Class
        {
            get
            {
                return _data.Class;
            }
        }

        /// <summary>
        /// Promotes the class from the old type to the new type and returns the new
        /// FakeExpandoData object.
        /// </summary>
        private FakeExpandoData PromoteClassCore(FakeExpandoClass oldClass, FakeExpandoClass newClass)
        {
            Debug.Assert(oldClass != newClass);

            lock (LockObject)
            {
                if (_data.Class == oldClass)
                {
                    _data = _data.UpdateClass(newClass);
                }
                return _data;
            }
        }

        /// <summary>
        /// Internal helper to promote a class.  Called from our RuntimeOps helper.  This
        /// version simply doesn't expose the FakeExpandoData object which is a private
        /// data structure.
        /// </summary>
        internal void PromoteClass(object oldClass, object newClass)
        {
            PromoteClassCore((FakeExpandoClass)oldClass, (FakeExpandoClass)newClass);
        }

        #endregion

        #region IDynamicMetaObjectProvider Members

        DynamicMetaObject IDynamicMetaObjectProvider.GetMetaObject(Expression parameter)
        {
            return new MetaExpando(parameter, this);
        }
        #endregion

        #region Helper methods
        private void TryAddMember(string key, object value)
        {
            // Pass null to the class, which forces lookup.
            TrySetValue(null, -1, value, key, false, true);
        }

        private bool TryGetValueForKey(string key, out object value)
        {
            // Pass null to the class, which forces lookup.
            return TryGetValue(null, -1, key, false, out value);
        }

        private bool ExpandoContainsKey(string key)
        {
            return _data.Class.GetValueIndexCaseSensitive(key) >= 0;
        }

        // We create a non-generic type for the debug view for each different collection type
        // that uses DebuggerTypeProxy, instead of defining a generic debug view type and
        // using different instantiations. The reason for this is that support for generics
        // with using DebuggerTypeProxy is limited. For C#, DebuggerTypeProxy supports only
        // open types (from MSDN http://msdn.microsoft.com/en-us/library/d8eyd8zc.aspx).
        private sealed class KeyCollectionDebugView
        {
            private ICollection<string> collection;
            public KeyCollectionDebugView(ICollection<string> collection)
            {
                Debug.Assert(collection != null);
                this.collection = collection;
            }

            [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
            public string[] Items
            {
                get
                {
                    string[] items = new string[collection.Count];
                    collection.CopyTo(items, 0);
                    return items;
                }
            }
        }

        [DebuggerTypeProxy(typeof(KeyCollectionDebugView))]
        [DebuggerDisplay("Count = {Count}")]
        private class KeyCollection : ICollection<string>
        {
            private readonly FakeExpandoObject _expando;
            private readonly int _expandoVersion;
            private readonly int _expandoCount;
            private readonly FakeExpandoData _FakeExpandoData;

            internal KeyCollection(FakeExpandoObject expando)
            {
                lock (expando.LockObject)
                {
                    _expando = expando;
                    _expandoVersion = expando._data.Version;
                    _expandoCount = expando._count;
                    _FakeExpandoData = expando._data;
                }
            }

            private void CheckVersion()
            {
                if (_expando._data.Version != _expandoVersion || _FakeExpandoData != _expando._data)
                {
                    //the underlying expando object has changed
                    throw new Exception();
                }
            }

            #region ICollection<string> Members

            public void Add(string item)
            {
                throw new Exception();
            }

            public void Clear()
            {
                throw new Exception();
            }

            public bool Contains(string item)
            {
                lock (_expando.LockObject)
                {
                    CheckVersion();
                    return _expando.ExpandoContainsKey(item);
                }
            }

            public void CopyTo(string[] array, int arrayIndex)
            {
                lock (_expando.LockObject)
                {
                    CheckVersion();
                    FakeExpandoData data = _expando._data;
                    for (int i = 0; i < data.Class.Keys.Length; i++)
                    {
                        if (data[i] != Uninitialized)
                        {
                            array[arrayIndex++] = data.Class.Keys[i];
                        }
                    }
                }
            }

            public int Count
            {
                get
                {
                    CheckVersion();
                    return _expandoCount;
                }
            }

            public bool IsReadOnly
            {
                get { return true; }
            }

            public bool Remove(string item)
            {
                throw new Exception();
            }

            #endregion

            #region IEnumerable<string> Members

            public IEnumerator<string> GetEnumerator()
            {
                for (int i = 0, n = _FakeExpandoData.Class.Keys.Length; i < n; i++)
                {
                    CheckVersion();
                    if (_FakeExpandoData[i] != Uninitialized)
                    {
                        yield return _FakeExpandoData.Class.Keys[i];
                    }
                }
            }

            #endregion

            #region IEnumerable Members

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            #endregion
        }

        // We create a non-generic type for the debug view for each different collection type
        // that uses DebuggerTypeProxy, instead of defining a generic debug view type and
        // using different instantiations. The reason for this is that support for generics
        // with using DebuggerTypeProxy is limited. For C#, DebuggerTypeProxy supports only
        // open types (from MSDN http://msdn.microsoft.com/en-us/library/d8eyd8zc.aspx).
        private sealed class ValueCollectionDebugView
        {
            private ICollection<object> collection;
            public ValueCollectionDebugView(ICollection<object> collection)
            {
                Debug.Assert(collection != null);
                this.collection = collection;
            }

            [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
            public object[] Items
            {
                get
                {
                    object[] items = new object[collection.Count];
                    collection.CopyTo(items, 0);
                    return items;
                }
            }
        }

        [DebuggerTypeProxy(typeof(ValueCollectionDebugView))]
        [DebuggerDisplay("Count = {Count}")]
        private class ValueCollection : ICollection<object>
        {
            private readonly FakeExpandoObject _expando;
            private readonly int _expandoVersion;
            private readonly int _expandoCount;
            private readonly FakeExpandoData _FakeExpandoData;

            internal ValueCollection(FakeExpandoObject expando)
            {
                lock (expando.LockObject)
                {
                    _expando = expando;
                    _expandoVersion = expando._data.Version;
                    _expandoCount = expando._count;
                    _FakeExpandoData = expando._data;
                }
            }

            private void CheckVersion()
            {
                if (_expando._data.Version != _expandoVersion || _FakeExpandoData != _expando._data)
                {
                    //the underlying expando object has changed
                    throw new Exception();
                }
            }

            #region ICollection<string> Members

            public void Add(object item)
            {
                throw new Exception();
            }

            public void Clear()
            {
                throw new Exception();
            }

            public bool Contains(object item)
            {
                lock (_expando.LockObject)
                {
                    CheckVersion();

                    FakeExpandoData data = _expando._data;
                    for (int i = 0; i < data.Class.Keys.Length; i++)
                    {

                        // See comment in TryDeleteValue; it's okay to call
                        // object.Equals with the lock held.
                        if (object.Equals(data[i], item))
                        {
                            return true;
                        }
                    }
                    return false;
                }
            }

            public void CopyTo(object[] array, int arrayIndex)
            {
                lock (_expando.LockObject)
                {
                    CheckVersion();
                    FakeExpandoData data = _expando._data;
                    for (int i = 0; i < data.Class.Keys.Length; i++)
                    {
                        if (data[i] != Uninitialized)
                        {
                            array[arrayIndex++] = data[i];
                        }
                    }
                }
            }

            public int Count
            {
                get
                {
                    CheckVersion();
                    return _expandoCount;
                }
            }

            public bool IsReadOnly
            {
                get { return true; }
            }

            public bool Remove(object item)
            {
                throw new Exception();
            }

            #endregion

            #region IEnumerable<string> Members

            public IEnumerator<object> GetEnumerator()
            {
                FakeExpandoData data = _expando._data;
                for (int i = 0; i < data.Class.Keys.Length; i++)
                {
                    CheckVersion();
                    // Capture the value into a temp so we don't inadvertently
                    // return Uninitialized.
                    object temp = data[i];
                    if (temp != Uninitialized)
                    {
                        yield return temp;
                    }
                }
            }

            #endregion

            #region IEnumerable Members

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            #endregion
        }

        #endregion

        #region IDictionary<string, object> Members
        ICollection<string> IDictionary<string, object>.Keys
        {
            get
            {
                return new KeyCollection(this);
            }
        }

        ICollection<object> IDictionary<string, object>.Values
        {
            get
            {
                return new ValueCollection(this);
            }
        }

        object IDictionary<string, object>.this[string key]
        {
            get
            {
                object value;
                if (!TryGetValueForKey(key, out value))
                {
                    throw new Exception();
                }
                return value;
            }
            set
            {
                // Pass null to the class, which forces lookup.
                TrySetValue(null, -1, value, key, false, false);
            }
        }

        void IDictionary<string, object>.Add(string key, object value)
        {
            this.TryAddMember(key, value);
        }

        bool IDictionary<string, object>.ContainsKey(string key)
        {
            FakeExpandoData data = _data;
            int index = data.Class.GetValueIndexCaseSensitive(key);
            return index >= 0 && data[index] != Uninitialized;
        }

        bool IDictionary<string, object>.Remove(string key)
        {
            // Pass null to the class, which forces lookup.
            return TryDeleteValue(null, -1, key, false, Uninitialized);
        }

        bool IDictionary<string, object>.TryGetValue(string key, out object value)
        {
            return TryGetValueForKey(key, out value);
        }

        #endregion

        #region ICollection<KeyValuePair<string, object>> Members
        int ICollection<KeyValuePair<string, object>>.Count
        {
            get
            {
                return _count;
            }
        }

        bool ICollection<KeyValuePair<string, object>>.IsReadOnly
        {
            get { return false; }
        }

        void ICollection<KeyValuePair<string, object>>.Add(KeyValuePair<string, object> item)
        {
            TryAddMember(item.Key, item.Value);
        }

        void ICollection<KeyValuePair<string, object>>.Clear()
        {
            // We remove both class and data!
            FakeExpandoData data;
            lock (LockObject)
            {
                data = _data;
                _data = FakeExpandoData.Empty;
                _count = 0;
            }

            // Notify property changed for all properties.
            var propertyChanged = _propertyChanged;
            if (propertyChanged != null)
            {
                for (int i = 0, n = data.Class.Keys.Length; i < n; i++)
                {
                    if (data[i] != Uninitialized)
                    {
                        propertyChanged(this, new PropertyChangedEventArgs(data.Class.Keys[i]));
                    }
                }
            }
        }

        bool ICollection<KeyValuePair<string, object>>.Contains(KeyValuePair<string, object> item)
        {
            object value;
            if (!TryGetValueForKey(item.Key, out value))
            {
                return false;
            }

            return object.Equals(value, item.Value);
        }

        void ICollection<KeyValuePair<string, object>>.CopyTo(KeyValuePair<string, object>[] array, int arrayIndex)
        {
            // We want this to be atomic and not throw
            lock (LockObject)
            {
                foreach (KeyValuePair<string, object> item in this)
                {
                    array[arrayIndex++] = item;
                }
            }
        }

        bool ICollection<KeyValuePair<string, object>>.Remove(KeyValuePair<string, object> item)
        {
            return TryDeleteValue(null, -1, item.Key, false, item.Value);
        }
        #endregion

        #region IEnumerable<KeyValuePair<string, object>> Member

        IEnumerator<KeyValuePair<string, object>> IEnumerable<KeyValuePair<string, object>>.GetEnumerator()
        {
            FakeExpandoData data = _data;
            return GetExpandoEnumerator(data, data.Version);
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            FakeExpandoData data = _data;
            return GetExpandoEnumerator(data, data.Version);
        }

        // Note: takes the data and version as parameters so they will be
        // captured before the first call to MoveNext().
        private IEnumerator<KeyValuePair<string, object>> GetExpandoEnumerator(FakeExpandoData data, int version)
        {
            for (int i = 0; i < data.Class.Keys.Length; i++)
            {
                if (_data.Version != version || data != _data)
                {
                    // The underlying expando object has changed:
                    // 1) the version of the expando data changed
                    // 2) the data object is changed 
                    throw new Exception();
                }
                // Capture the value into a temp so we don't inadvertently
                // return Uninitialized.
                object temp = data[i];
                if (temp != Uninitialized)
                {
                    yield return new KeyValuePair<string, object>(data.Class.Keys[i], temp);
                }
            }
        }
        #endregion

        #region MetaExpando

        private class MetaExpando : DynamicMetaObject
        {
            public MetaExpando(Expression expression, FakeExpandoObject value)
                : base(expression, BindingRestrictions.Empty, value)
            {
            }

            private DynamicMetaObject BindGetOrInvokeMember(DynamicMetaObjectBinder binder, string name, bool ignoreCase, DynamicMetaObject fallback, Func<DynamicMetaObject, DynamicMetaObject> fallbackInvoke)
            {
                FakeExpandoClass klass = Value.Class;

                //try to find the member, including the deleted members
                int index = klass.GetValueIndex(name, ignoreCase, Value);

                ParameterExpression value = Expression.Parameter(typeof(object), "value");

                Expression tryGetValue = Expression.Call(
                    typeof(RuntimeOps).GetMethod("ExpandoTryGetValue"),
                    GetLimitedSelf(),
                    Expression.Constant(klass, typeof(object)),
                    Expression.Constant(index),
                    Expression.Constant(name),
                    Expression.Constant(ignoreCase),
                    value
                );

                var result = new DynamicMetaObject(value, BindingRestrictions.Empty);
                if (fallbackInvoke != null)
                {
                    result = fallbackInvoke(result);
                }

                result = new DynamicMetaObject(
                    Expression.Block(
                        new[] { value },
                        Expression.Condition(
                            tryGetValue,
                            result.Expression,
                            fallback.Expression,
                            typeof(object)
                        )
                    ),
                    result.Restrictions.Merge(fallback.Restrictions)
                );

                return AddDynamicTestAndDefer(binder, Value.Class, null, result);
            }

            public override DynamicMetaObject BindGetMember(GetMemberBinder binder)
            {
                return BindGetOrInvokeMember(
                    binder,
                    binder.Name,
                    binder.IgnoreCase,
                    binder.FallbackGetMember(this),
                    null
                );
            }

            public override DynamicMetaObject BindInvokeMember(InvokeMemberBinder binder, DynamicMetaObject[] args)
            {
                return BindGetOrInvokeMember(
                    binder,
                    binder.Name,
                    binder.IgnoreCase,
                    binder.FallbackInvokeMember(this, args),
                    value => binder.FallbackInvoke(value, args, null)
                );
            }

            public override DynamicMetaObject BindSetMember(SetMemberBinder binder, DynamicMetaObject value)
            {
                FakeExpandoClass klass;
                int index;

                FakeExpandoClass originalClass = GetClassEnsureIndex(binder.Name, binder.IgnoreCase, Value, out klass, out index);

                return AddDynamicTestAndDefer(
                    binder,
                    klass,
                    originalClass,
                    new DynamicMetaObject(
                        Expression.Call(
                            typeof(RuntimeOps).GetMethod("ExpandoTrySetValue"),
                            GetLimitedSelf(),
                            Expression.Constant(klass, typeof(object)),
                            Expression.Constant(index),
                            Expression.Convert(value.Expression, typeof(object)),
                            Expression.Constant(binder.Name),
                            Expression.Constant(binder.IgnoreCase)
                        ),
                        BindingRestrictions.Empty
                    )
                );
            }

            public override DynamicMetaObject BindDeleteMember(DeleteMemberBinder binder)
            {
                int index = Value.Class.GetValueIndex(binder.Name, binder.IgnoreCase, Value);

                Expression tryDelete = Expression.Call(
                    typeof(RuntimeOps).GetMethod("ExpandoTryDeleteValue"),
                    GetLimitedSelf(),
                    Expression.Constant(Value.Class, typeof(object)),
                    Expression.Constant(index),
                    Expression.Constant(binder.Name),
                    Expression.Constant(binder.IgnoreCase)
                );
                DynamicMetaObject fallback = binder.FallbackDeleteMember(this);

                DynamicMetaObject target = new DynamicMetaObject(
                    Expression.IfThen(Expression.Not(tryDelete), fallback.Expression),
                    fallback.Restrictions
                );

                return AddDynamicTestAndDefer(binder, Value.Class, null, target);
            }

            public override IEnumerable<string> GetDynamicMemberNames()
            {
                var FakeExpandoData = Value._data;
                var klass = FakeExpandoData.Class;
                for (int i = 0; i < klass.Keys.Length; i++)
                {
                    object val = FakeExpandoData[i];
                    if (val != FakeExpandoObject.Uninitialized)
                    {
                        yield return klass.Keys[i];
                    }
                }
            }

            /// <summary>
            /// Adds a dynamic test which checks if the version has changed.  The test is only necessary for
            /// performance as the methods will do the correct thing if called with an incorrect version.
            /// </summary>
            private DynamicMetaObject AddDynamicTestAndDefer(DynamicMetaObjectBinder binder, FakeExpandoClass klass, FakeExpandoClass originalClass, DynamicMetaObject succeeds)
            {

                Expression ifTestSucceeds = succeeds.Expression;
                if (originalClass != null)
                {
                    // we are accessing a member which has not yet been defined on this class.
                    // We force a class promotion after the type check.  If the class changes the 
                    // promotion will fail and the set/delete will do a full lookup using the new
                    // class to discover the name.
                    Debug.Assert(originalClass != klass);

                    ifTestSucceeds = Expression.Block(
                        Expression.Call(
                            null,
                            typeof(RuntimeOps).GetMethod("ExpandoPromoteClass"),
                            GetLimitedSelf(),
                            Expression.Constant(originalClass, typeof(object)),
                            Expression.Constant(klass, typeof(object))
                        ),
                        succeeds.Expression
                    );
                }

                return new DynamicMetaObject(
                    Expression.Condition(
                        Expression.Call(
                            null,
                            typeof(RuntimeOps).GetMethod("ExpandoCheckVersion"),
                            GetLimitedSelf(),
                            Expression.Constant(originalClass ?? klass, typeof(object))
                        ),
                        ifTestSucceeds,
                        binder.GetUpdateExpression(ifTestSucceeds.Type)
                    ),
                    GetRestrictions().Merge(succeeds.Restrictions)
                );
            }

            /// <summary>
            /// Gets the class and the index associated with the given name.  Does not update the expando object.  Instead
            /// this returns both the original and desired new class.  A rule is created which includes the test for the
            /// original class, the promotion to the new class, and the set/delete based on the class post-promotion.
            /// </summary>
            private FakeExpandoClass GetClassEnsureIndex(string name, bool caseInsensitive, FakeExpandoObject obj, out FakeExpandoClass klass, out int index)
            {
                FakeExpandoClass originalClass = Value.Class;

                index = originalClass.GetValueIndex(name, caseInsensitive, obj);
                if (index == FakeExpandoObject.AmbiguousMatchFound)
                {
                    klass = originalClass;
                    return null;
                }
                if (index == FakeExpandoObject.NoMatch)
                {
                    // go ahead and find a new class now...
                    FakeExpandoClass newClass = originalClass.FindNewClass(name);

                    klass = newClass;
                    index = newClass.GetValueIndexCaseSensitive(name);

                    Debug.Assert(index != FakeExpandoObject.NoMatch);
                    return originalClass;
                }
                else
                {
                    klass = originalClass;
                    return null;
                }
            }

            /// <summary>
            /// Returns our Expression converted to our known LimitType
            /// </summary>
            private Expression GetLimitedSelf()
            {
                if (TypeUtils.AreEquivalent(Expression.Type, LimitType))
                {
                    return Expression;
                }
                return Expression.Convert(Expression, LimitType);
            }

            /// <summary>
            /// Returns a Restrictions object which includes our current restrictions merged
            /// with a restriction limiting our type
            /// </summary>
            private BindingRestrictions GetRestrictions()
            {
                Debug.Assert(Restrictions == BindingRestrictions.Empty, "We don't merge, restrictions are always empty");

                if (Value == null && HasValue)
                {
                    return BindingRestrictions.GetInstanceRestriction(Expression, null);
                }
                else
                {
                    return BindingRestrictions.GetTypeRestriction(Expression, LimitType);
                }
            }

            public new FakeExpandoObject Value
            {
                get
                {
                    return (FakeExpandoObject)base.Value;
                }
            }
        }

        #endregion

        #region FakeExpandoData

        /// <summary>
        /// Stores the class and the data associated with the class as one atomic
        /// pair.  This enables us to do a class check in a thread safe manner w/o
        /// requiring locks.
        /// </summary>
        private class FakeExpandoData
        {
            internal static FakeExpandoData Empty = new FakeExpandoData();

            /// <summary>
            /// the dynamically assigned class associated with the Expando object
            /// </summary>
            internal readonly FakeExpandoClass Class;

            /// <summary>
            /// data stored in the expando object, key names are stored in the class.
            /// 
            /// Expando._data must be locked when mutating the value.  Otherwise a copy of it 
            /// could be made and lose values.
            /// </summary>
            private readonly object[] _dataArray;

            /// <summary>
            /// Indexer for getting/setting the data
            /// </summary>
            internal object this[int index]
            {
                get
                {
                    return _dataArray[index];
                }
                set
                {
                    //when the array is updated, version increases, even the new value is the same
                    //as previous. Dictionary type has the same behavior.
                    _version++;
                    _dataArray[index] = value;
                }
            }

            internal int Version
            {
                get { return _version; }
            }

            internal int Length
            {
                get { return _dataArray.Length; }
            }

            /// <summary>
            /// Constructs an empty FakeExpandoData object with the empty class and no data.
            /// </summary>
            private FakeExpandoData()
            {
                Class = FakeExpandoClass.Empty;
                _dataArray = new object[0];
            }

            /// <summary>
            /// the version of the ExpandoObject that tracks set and delete operations
            /// </summary>
            private int _version;

            /// <summary>
            /// Constructs a new FakeExpandoData object with the specified class and data.
            /// </summary>
            internal FakeExpandoData(FakeExpandoClass klass, object[] data, int version)
            {
                Class = klass;
                _dataArray = data;
                _version = version;
            }

            /// <summary>
            /// Update the associated class and increases the storage for the data array if needed.
            /// </summary>
            /// <returns></returns>
            internal FakeExpandoData UpdateClass(FakeExpandoClass newClass)
            {
                if (_dataArray.Length >= newClass.Keys.Length)
                {
                    // we have extra space in our buffer, just initialize it to Uninitialized.
                    this[newClass.Keys.Length - 1] = FakeExpandoObject.Uninitialized;
                    return new FakeExpandoData(newClass, this._dataArray, this._version);
                }
                else
                {
                    // we've grown too much - we need a new object array
                    int oldLength = _dataArray.Length;
                    object[] arr = new object[GetAlignedSize(newClass.Keys.Length)];
                    Array.Copy(_dataArray, arr, _dataArray.Length);
                    FakeExpandoData newData = new FakeExpandoData(newClass, arr, this._version);
                    newData[oldLength] = FakeExpandoObject.Uninitialized;
                    return newData;
                }
            }

            private static int GetAlignedSize(int len)
            {
                // the alignment of the array for storage of values (must be a power of two)
                const int DataArrayAlignment = 8;

                // round up and then mask off lower bits
                return (len + (DataArrayAlignment - 1)) & (~(DataArrayAlignment - 1));
            }
        }

        #endregion

        #region INotifyPropertyChanged Members

        event PropertyChangedEventHandler INotifyPropertyChanged.PropertyChanged
        {
            add { _propertyChanged += value; }
            remove { _propertyChanged -= value; }
        }

        #endregion
    }
}

namespace System.Runtime.CompilerServices
{

    //
    // Note: these helpers are kept as simple wrappers so they have a better 
    // chance of being inlined.
    //
    public static partial class RuntimeOps
    {

        /// <summary>
        /// Gets the value of an item in an expando object.
        /// </summary>
        /// <param name="expando">The expando object.</param>
        /// <param name="indexClass">The class of the expando object.</param>
        /// <param name="index">The index of the member.</param>
        /// <param name="name">The name of the member.</param>
        /// <param name="ignoreCase">true if the name should be matched ignoring case; false otherwise.</param>
        /// <param name="value">The out parameter containing the value of the member.</param>
        /// <returns>True if the member exists in the expando object, otherwise false.</returns>
        [Obsolete("do not use this method", true), EditorBrowsable(EditorBrowsableState.Never)]
        public static bool ExpandoTryGetValue(FakeExpandoObject expando, object indexClass, int index, string name, bool ignoreCase, out object value)
        {
            return expando.TryGetValue(indexClass, index, name, ignoreCase, out value);
        }

        /// <summary>
        /// Sets the value of an item in an expando object.
        /// </summary>
        /// <param name="expando">The expando object.</param>
        /// <param name="indexClass">The class of the expando object.</param>
        /// <param name="index">The index of the member.</param>
        /// <param name="value">The value of the member.</param>
        /// <param name="name">The name of the member.</param>
        /// <param name="ignoreCase">true if the name should be matched ignoring case; false otherwise.</param>
        /// <returns>
        /// Returns the index for the set member.
        /// </returns>
        [Obsolete("do not use this method", true), EditorBrowsable(EditorBrowsableState.Never)]
        public static object ExpandoTrySetValue(FakeExpandoObject expando, object indexClass, int index, object value, string name, bool ignoreCase)
        {
            expando.TrySetValue(indexClass, index, value, name, ignoreCase, false);
            return value;
        }

        /// <summary>
        /// Deletes the value of an item in an expando object.
        /// </summary>
        /// <param name="expando">The expando object.</param>
        /// <param name="indexClass">The class of the expando object.</param>
        /// <param name="index">The index of the member.</param>
        /// <param name="name">The name of the member.</param>
        /// <param name="ignoreCase">true if the name should be matched ignoring case; false otherwise.</param>
        /// <returns>true if the item was successfully removed; otherwise, false.</returns>
        [Obsolete("do not use this method", true), EditorBrowsable(EditorBrowsableState.Never)]
        public static bool ExpandoTryDeleteValue(FakeExpandoObject expando, object indexClass, int index, string name, bool ignoreCase)
        {
            return expando.TryDeleteValue(indexClass, index, name, ignoreCase, FakeExpandoObject.Uninitialized);
        }

        /// <summary>
        /// Checks the version of the expando object.
        /// </summary>
        /// <param name="expando">The expando object.</param>
        /// <param name="version">The version to check.</param>
        /// <returns>true if the version is equal; otherwise, false.</returns>
        [Obsolete("do not use this method", true), EditorBrowsable(EditorBrowsableState.Never)]
        public static bool ExpandoCheckVersion(FakeExpandoObject expando, object version)
        {
            return expando.Class == version;
        }

        /// <summary>
        /// Promotes an expando object from one class to a new class.
        /// </summary>
        /// <param name="expando">The expando object.</param>
        /// <param name="oldClass">The old class of the expando object.</param>
        /// <param name="newClass">The new class of the expando object.</param>
        [Obsolete("do not use this method", true), EditorBrowsable(EditorBrowsableState.Never)]
        public static void ExpandoPromoteClass(FakeExpandoObject expando, object oldClass, object newClass)
        {
            expando.PromoteClass(oldClass, newClass);
        }
    }

    internal static class TypeUtils
    {
        private const BindingFlags AnyStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        internal const MethodAttributes PublicStatic = MethodAttributes.Public | MethodAttributes.Static;

        internal static Type GetNonNullableType(this Type type)
        {
            if (IsNullableType(type))
            {
                return type.GetGenericArguments()[0];
            }
            return type;
        }

        internal static Type GetNullableType(Type type)
        {
            Debug.Assert(type != null, "type cannot be null");
            if (type.IsValueType && !IsNullableType(type))
            {
                return typeof(Nullable<>).MakeGenericType(type);
            }
            return type;
        }

        internal static bool IsNullableType(this Type type)
        {
            return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>);
        }

        internal static bool IsBool(Type type)
        {
            return GetNonNullableType(type) == typeof(bool);
        }

        internal static bool IsNumeric(Type type)
        {
            type = GetNonNullableType(type);
            if (!type.IsEnum)
            {
                switch (Type.GetTypeCode(type))
                {
                    case TypeCode.Char:
                    case TypeCode.SByte:
                    case TypeCode.Byte:
                    case TypeCode.Int16:
                    case TypeCode.Int32:
                    case TypeCode.Int64:
                    case TypeCode.Double:
                    case TypeCode.Single:
                    case TypeCode.UInt16:
                    case TypeCode.UInt32:
                    case TypeCode.UInt64:
                        return true;
                }
            }
            return false;
        }

        internal static bool IsInteger(Type type)
        {
            type = GetNonNullableType(type);
            if (type.IsEnum)
            {
                return false;
            }
            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Byte:
                case TypeCode.SByte:
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                    return true;
                default:
                    return false;
            }
        }


        internal static bool IsArithmetic(Type type)
        {
            type = GetNonNullableType(type);
            if (!type.IsEnum)
            {
                switch (Type.GetTypeCode(type))
                {
                    case TypeCode.Int16:
                    case TypeCode.Int32:
                    case TypeCode.Int64:
                    case TypeCode.Double:
                    case TypeCode.Single:
                    case TypeCode.UInt16:
                    case TypeCode.UInt32:
                    case TypeCode.UInt64:
                        return true;
                }
            }
            return false;
        }

        internal static bool IsUnsignedInt(Type type)
        {
            type = GetNonNullableType(type);
            if (!type.IsEnum)
            {
                switch (Type.GetTypeCode(type))
                {
                    case TypeCode.UInt16:
                    case TypeCode.UInt32:
                    case TypeCode.UInt64:
                        return true;
                }
            }
            return false;
        }

        internal static bool IsIntegerOrBool(Type type)
        {
            type = GetNonNullableType(type);
            if (!type.IsEnum)
            {
                switch (Type.GetTypeCode(type))
                {
                    case TypeCode.Int64:
                    case TypeCode.Int32:
                    case TypeCode.Int16:
                    case TypeCode.UInt64:
                    case TypeCode.UInt32:
                    case TypeCode.UInt16:
                    case TypeCode.Boolean:
                    case TypeCode.SByte:
                    case TypeCode.Byte:
                        return true;
                }
            }
            return false;
        }

        internal static bool AreEquivalent(Type t1, Type t2)
        {
#if CLR2 || SILVERLIGHT
            return t1 == t2;
#else
            return t1 == t2 || t1.IsEquivalentTo(t2);
#endif
        }

        internal static bool AreReferenceAssignable(Type dest, Type src)
        {
            // WARNING: This actually implements "Is this identity assignable and/or reference assignable?"
            if (AreEquivalent(dest, src))
            {
                return true;
            }
            if (!dest.IsValueType && !src.IsValueType && dest.IsAssignableFrom(src))
            {
                return true;
            }
            return false;
        }

        // Checks if the type is a valid target for an instance call
        internal static bool IsValidInstanceType(MemberInfo member, Type instanceType)
        {
            Type targetType = member.DeclaringType;
            if (AreReferenceAssignable(targetType, instanceType))
            {
                return true;
            }
            if (instanceType.IsValueType)
            {
                if (AreReferenceAssignable(targetType, typeof(System.Object)))
                {
                    return true;
                }
                if (AreReferenceAssignable(targetType, typeof(System.ValueType)))
                {
                    return true;
                }
                if (instanceType.IsEnum && AreReferenceAssignable(targetType, typeof(System.Enum)))
                {
                    return true;
                }
                // A call to an interface implemented by a struct is legal whether the struct has
                // been boxed or not.
                if (targetType.IsInterface)
                {
                    foreach (Type interfaceType in instanceType.GetInterfaces())
                    {
                        if (AreReferenceAssignable(targetType, interfaceType))
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        internal static bool HasIdentityPrimitiveOrNullableConversion(Type source, Type dest)
        {
            Debug.Assert(source != null && dest != null);

            // Identity conversion
            if (AreEquivalent(source, dest))
            {
                return true;
            }

            // Nullable conversions
            if (IsNullableType(source) && AreEquivalent(dest, GetNonNullableType(source)))
            {
                return true;
            }
            if (IsNullableType(dest) && AreEquivalent(source, GetNonNullableType(dest)))
            {
                return true;
            }
            // Primitive runtime conversions
            // All conversions amongst enum, bool, char, integer and float types
            // (and their corresponding nullable types) are legal except for
            // nonbool==>bool and nonbool==>bool?
            // Since we have already covered bool==>bool, bool==>bool?, etc, above,
            // we can just disallow having a bool or bool? destination type here.
            if (IsConvertible(source) && IsConvertible(dest) && GetNonNullableType(dest) != typeof(bool))
            {
                return true;
            }
            return false;
        }

        internal static bool HasReferenceConversion(Type source, Type dest)
        {
            Debug.Assert(source != null && dest != null);

            // void -> void conversion is handled elsewhere
            // (it's an identity conversion)
            // All other void conversions are disallowed.
            if (source == typeof(void) || dest == typeof(void))
            {
                return false;
            }

            Type nnSourceType = TypeUtils.GetNonNullableType(source);
            Type nnDestType = TypeUtils.GetNonNullableType(dest);

            // Down conversion
            if (nnSourceType.IsAssignableFrom(nnDestType))
            {
                return true;
            }
            // Up conversion
            if (nnDestType.IsAssignableFrom(nnSourceType))
            {
                return true;
            }
            // Interface conversion
            if (source.IsInterface || dest.IsInterface)
            {
                return true;
            }
            // Variant delegate conversion
            if (IsLegalExplicitVariantDelegateConversion(source, dest))
                return true;

            // Object conversion
            if (source == typeof(object) || dest == typeof(object))
            {
                return true;
            }
            return false;
        }

        private static bool IsCovariant(Type t)
        {
            Debug.Assert(t != null);
            return 0 != (t.GenericParameterAttributes & GenericParameterAttributes.Covariant);
        }

        private static bool IsContravariant(Type t)
        {
            Debug.Assert(t != null);
            return 0 != (t.GenericParameterAttributes & GenericParameterAttributes.Contravariant);
        }

        private static bool IsInvariant(Type t)
        {
            Debug.Assert(t != null);
            return 0 == (t.GenericParameterAttributes & GenericParameterAttributes.VarianceMask);
        }

        private static bool IsDelegate(Type t)
        {
            Debug.Assert(t != null);
            return t.IsSubclassOf(typeof(System.MulticastDelegate));
        }

        internal static bool IsLegalExplicitVariantDelegateConversion(Type source, Type dest)
        {
            Debug.Assert(source != null && dest != null);

            // There *might* be a legal conversion from a generic delegate type S to generic delegate type  T, 
            // provided all of the follow are true:
            //   o Both types are constructed generic types of the same generic delegate type, D<X1,... Xk>.
            //     That is, S = D<S1...>, T = D<T1...>.
            //   o If type parameter Xi is declared to be invariant then Si must be identical to Ti.
            //   o If type parameter Xi is declared to be covariant ("out") then Si must be convertible
            //     to Ti via an identify conversion,  implicit reference conversion, or explicit reference conversion.
            //   o If type parameter Xi is declared to be contravariant ("in") then either Si must be identical to Ti, 
            //     or Si and Ti must both be reference types.

            if (!IsDelegate(source) || !IsDelegate(dest) || !source.IsGenericType || !dest.IsGenericType)
                return false;

            Type genericDelegate = source.GetGenericTypeDefinition();

            if (dest.GetGenericTypeDefinition() != genericDelegate)
                return false;

            Type[] genericParameters = genericDelegate.GetGenericArguments();
            Type[] sourceArguments = source.GetGenericArguments();
            Type[] destArguments = dest.GetGenericArguments();

            Debug.Assert(genericParameters != null);
            Debug.Assert(sourceArguments != null);
            Debug.Assert(destArguments != null);
            Debug.Assert(genericParameters.Length == sourceArguments.Length);
            Debug.Assert(genericParameters.Length == destArguments.Length);

            for (int iParam = 0; iParam < genericParameters.Length; ++iParam)
            {
                Type sourceArgument = sourceArguments[iParam];
                Type destArgument = destArguments[iParam];

                Debug.Assert(sourceArgument != null && destArgument != null);

                // If the arguments are identical then this one is automatically good, so skip it.
                if (AreEquivalent(sourceArgument, destArgument))
                {
                    continue;
                }

                Type genericParameter = genericParameters[iParam];

                Debug.Assert(genericParameter != null);

                if (IsInvariant(genericParameter))
                {
                    return false;
                }

                if (IsCovariant(genericParameter))
                {
                    if (!HasReferenceConversion(sourceArgument, destArgument))
                    {
                        return false;
                    }
                }
                else if (IsContravariant(genericParameter))
                {
                    if (sourceArgument.IsValueType || destArgument.IsValueType)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        internal static bool IsConvertible(Type type)
        {
            type = GetNonNullableType(type);
            if (type.IsEnum)
            {
                return true;
            }
            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Boolean:
                case TypeCode.Byte:
                case TypeCode.SByte:
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                case TypeCode.Single:
                case TypeCode.Double:
                case TypeCode.Char:
                    return true;
                default:
                    return false;
            }
        }

        internal static bool HasReferenceEquality(Type left, Type right)
        {
            if (left.IsValueType || right.IsValueType)
            {
                return false;
            }

            // If we have an interface and a reference type then we can do 
            // reference equality.

            // If we have two reference types and one is assignable to the
            // other then we can do reference equality.

            return left.IsInterface || right.IsInterface ||
                AreReferenceAssignable(left, right) ||
                AreReferenceAssignable(right, left);
        }

        internal static bool HasBuiltInEqualityOperator(Type left, Type right)
        {
            // If we have an interface and a reference type then we can do 
            // reference equality.
            if (left.IsInterface && !right.IsValueType)
            {
                return true;
            }
            if (right.IsInterface && !left.IsValueType)
            {
                return true;
            }
            // If we have two reference types and one is assignable to the
            // other then we can do reference equality.
            if (!left.IsValueType && !right.IsValueType)
            {
                if (AreReferenceAssignable(left, right) || AreReferenceAssignable(right, left))
                {
                    return true;
                }
            }
            // Otherwise, if the types are not the same then we definitely 
            // do not have a built-in equality operator.
            if (!AreEquivalent(left, right))
            {
                return false;
            }
            // We have two identical value types, modulo nullability.  (If they were both the 
            // same reference type then we would have returned true earlier.)
            Debug.Assert(left.IsValueType);
            // Equality between struct types is only defined for numerics, bools, enums,
            // and their nullable equivalents.
            Type nnType = GetNonNullableType(left);
            if (nnType == typeof(bool) || IsNumeric(nnType) || nnType.IsEnum)
            {
                return true;
            }
            return false;
        }

        internal static bool IsImplicitlyConvertible(Type source, Type destination)
        {
            return AreEquivalent(source, destination) ||                // identity conversion
                IsImplicitNumericConversion(source, destination) ||
                IsImplicitReferenceConversion(source, destination) ||
                IsImplicitBoxingConversion(source, destination) ||
                IsImplicitNullableConversion(source, destination);
        }




        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity")]
        private static bool IsImplicitNumericConversion(Type source, Type destination)
        {
            TypeCode tcSource = Type.GetTypeCode(source);
            TypeCode tcDest = Type.GetTypeCode(destination);

            switch (tcSource)
            {
                case TypeCode.SByte:
                    switch (tcDest)
                    {
                        case TypeCode.Int16:
                        case TypeCode.Int32:
                        case TypeCode.Int64:
                        case TypeCode.Single:
                        case TypeCode.Double:
                        case TypeCode.Decimal:
                            return true;
                    }
                    return false;
                case TypeCode.Byte:
                    switch (tcDest)
                    {
                        case TypeCode.Int16:
                        case TypeCode.UInt16:
                        case TypeCode.Int32:
                        case TypeCode.UInt32:
                        case TypeCode.Int64:
                        case TypeCode.UInt64:
                        case TypeCode.Single:
                        case TypeCode.Double:
                        case TypeCode.Decimal:
                            return true;
                    }
                    return false;
                case TypeCode.Int16:
                    switch (tcDest)
                    {
                        case TypeCode.Int32:
                        case TypeCode.Int64:
                        case TypeCode.Single:
                        case TypeCode.Double:
                        case TypeCode.Decimal:
                            return true;
                    }
                    return false;
                case TypeCode.UInt16:
                    switch (tcDest)
                    {
                        case TypeCode.Int32:
                        case TypeCode.UInt32:
                        case TypeCode.Int64:
                        case TypeCode.UInt64:
                        case TypeCode.Single:
                        case TypeCode.Double:
                        case TypeCode.Decimal:
                            return true;
                    }
                    return false;
                case TypeCode.Int32:
                    switch (tcDest)
                    {
                        case TypeCode.Int64:
                        case TypeCode.Single:
                        case TypeCode.Double:
                        case TypeCode.Decimal:
                            return true;
                    }
                    return false;
                case TypeCode.UInt32:
                    switch (tcDest)
                    {
                        case TypeCode.UInt32:
                        case TypeCode.UInt64:
                        case TypeCode.Single:
                        case TypeCode.Double:
                        case TypeCode.Decimal:
                            return true;
                    }
                    return false;
                case TypeCode.Int64:
                case TypeCode.UInt64:
                    switch (tcDest)
                    {
                        case TypeCode.Single:
                        case TypeCode.Double:
                        case TypeCode.Decimal:
                            return true;
                    }
                    return false;
                case TypeCode.Char:
                    switch (tcDest)
                    {
                        case TypeCode.UInt16:
                        case TypeCode.Int32:
                        case TypeCode.UInt32:
                        case TypeCode.Int64:
                        case TypeCode.UInt64:
                        case TypeCode.Single:
                        case TypeCode.Double:
                        case TypeCode.Decimal:
                            return true;
                    }
                    return false;
                case TypeCode.Single:
                    return (tcDest == TypeCode.Double);
            }
            return false;
        }

        private static bool IsImplicitReferenceConversion(Type source, Type destination)
        {
            return destination.IsAssignableFrom(source);
        }

        private static bool IsImplicitBoxingConversion(Type source, Type destination)
        {
            if (source.IsValueType && (destination == typeof(object) || destination == typeof(System.ValueType)))
                return true;
            if (source.IsEnum && destination == typeof(System.Enum))
                return true;
            return false;
        }

        private static bool IsImplicitNullableConversion(Type source, Type destination)
        {
            if (IsNullableType(destination))
                return IsImplicitlyConvertible(GetNonNullableType(source), GetNonNullableType(destination));
            return false;
        }

        internal static bool IsSameOrSubclass(Type type, Type subType)
        {
            return AreEquivalent(type, subType) || subType.IsSubclassOf(type);
        }

        internal static void ValidateType(Type type)
        {
            if (type.IsGenericTypeDefinition)
            {
                throw new Exception();
            }
            if (type.ContainsGenericParameters)
            {
                throw new Exception();
            }
        }

        //from TypeHelper
        internal static Type FindGenericType(Type definition, Type type)
        {
            while (type != null && type != typeof(object))
            {
                if (type.IsGenericType && AreEquivalent(type.GetGenericTypeDefinition(), definition))
                {
                    return type;
                }
                if (definition.IsInterface)
                {
                    foreach (Type itype in type.GetInterfaces())
                    {
                        Type found = FindGenericType(definition, itype);
                        if (found != null)
                            return found;
                    }
                }
                type = type.BaseType;
            }
            return null;
        }

        internal static bool IsUnsigned(Type type)
        {
            type = GetNonNullableType(type);
            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Byte:
                case TypeCode.UInt16:
                case TypeCode.Char:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                    return true;
                default:
                    return false;
            }
        }

        internal static bool IsFloatingPoint(Type type)
        {
            type = GetNonNullableType(type);
            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Single:
                case TypeCode.Double:
                    return true;
                default:
                    return false;
            }
        }

        internal static Type GetNonRefType(this Type type)
        {
            return type.IsByRef ? type.GetElementType() : type;
        }

        private static readonly Assembly _mscorlib = typeof(object).Assembly;
        private static readonly Assembly _systemCore = typeof(Expression).Assembly;

        /// <summary>
        /// We can cache references to types, as long as they aren't in
        /// collectable assemblies. Unfortunately, we can't really distinguish
        /// between different flavors of assemblies. But, we can at least
        /// create a ---- for types in mscorlib (so we get the primitives)
        /// and System.Core (so we find Func/Action overloads, etc).
        /// </summary>
        internal static bool CanCache(this Type t)
        {
            // Note: we don't have to scan base or declaring types here.
            // There's no way for a type in mscorlib to derive from or be
            // contained in a type from another assembly. The only thing we
            // need to look at is the generic arguments, which are the thing
            // that allows mscorlib types to be specialized by types in other
            // assemblies.

            var asm = t.Assembly;
            if (asm != _mscorlib && asm != _systemCore)
            {
                // Not in mscorlib or our assembly
                return false;
            }

            if (t.IsGenericType)
            {
                foreach (Type g in t.GetGenericArguments())
                {
                    if (!CanCache(g))
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }
}
