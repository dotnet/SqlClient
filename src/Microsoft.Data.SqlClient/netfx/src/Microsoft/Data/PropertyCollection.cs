//------------------------------------------------------------------------------
// <copyright file="PropertyCollection.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>                                                                
// <owner current="true" primary="true">amirhmy</owner>
// <owner current="true" primary="false">markash</owner>
// <owner current="false" primary="false">jasonzhu</owner>
//------------------------------------------------------------------------------

namespace Microsoft.Data {
    using System;
    using System.Collections;
    using System.Runtime.Serialization;

    /// <devdoc>
    /// <para>Represents a collection of properties that can be added to <see cref='Microsoft.Data.DataColumn'/>, 
    /// <see cref='System.Data.DataSet'/>, 
    ///    or <see cref='Microsoft.Data.DataTable'/>.</para>
    /// </devdoc>
    [Serializable]
    public class PropertyCollection : Hashtable {
        public PropertyCollection() : base() {
        }
        
        protected PropertyCollection(SerializationInfo info, StreamingContext context) : base(info, context) {
        }

        public override object Clone() {
            // override Clone so that returned object is an
            // instance of PropertyCollection instead of Hashtable
            PropertyCollection clone = new PropertyCollection();
            foreach (DictionaryEntry pair in this) {
                clone.Add(pair.Key, pair.Value);
            }
            return clone;
        }
    }
    //3 NOTE: This should have been named PropertyDictionary, to avoid fxcop warnings about not having strongly typed IList and ICollection implementations, but it's too late now...
}
