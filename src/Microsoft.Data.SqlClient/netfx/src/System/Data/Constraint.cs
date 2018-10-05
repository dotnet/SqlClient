//------------------------------------------------------------------------------
// <copyright file="Constraint.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>                                                                
// <owner current="true" primary="true">amirhmy</owner>
// <owner current="true" primary="false">markash</owner>
// <owner current="false" primary="false">jasonzhu</owner>
//------------------------------------------------------------------------------

namespace Microsoft.Data {
    using System;
    using System.ComponentModel;
    using System.Globalization;

    /// <devdoc>
    ///    <para>Represents a constraint that can be enforced on one or
    ///       more <see cref='Microsoft.Data.DataColumn'/> objects.</para>
    /// </devdoc>
    [
    DefaultProperty("ConstraintName"),
    TypeConverter(typeof(ConstraintConverter))
    ]
    public abstract class Constraint {
        internal String name = "";
        private String _schemaName = "";
        private bool inCollection = false;
        private DataSet dataSet = null;

        internal PropertyCollection extendedProperties = null;

        /// <devdoc>
        /// <para>The name of this constraint within the <see cref='System.Data.ConstraintCollection'/> 
        /// .</para>
        /// </devdoc>
        [
        DefaultValue(""),
        ResCategoryAttribute(ResHelper.ResourceNames.DataCategory_Data),
        ResDescriptionAttribute(ResHelper.ResourceNames.ConstraintNameDescr)
        ]
        public virtual string ConstraintName {
            get {
                return name;
            }
            set {
                if (value == null)
                    value = "";

                if (Common.ADP.IsEmpty(value) && (Table != null) && InCollection)
                    throw ExceptionBuilder.NoConstraintName();

                CultureInfo locale = (Table != null ? Table.Locale : CultureInfo.CurrentCulture);
                if (String.Compare(name, value, true, locale) != 0) {
                    if ((Table != null) && InCollection) {
                        Table.Constraints.RegisterName(value);
                        if (name.Length != 0)
                            Table.Constraints.UnregisterName(name);
                    }
                    name = value;
                }
                else if (String.Compare(name, value, false, locale) != 0) {
                    name = value;
                }
            }
        }

        internal String SchemaName {
            get {
                if (Common.ADP.IsEmpty(_schemaName))
                    return ConstraintName;
                else
                    return _schemaName;
            }

            set {
                if (!Common.ADP.IsEmpty(value))
                    _schemaName = value;
            }
        }


        internal virtual bool InCollection {
            get {		// ACCESSOR: virtual was missing from this get
                return inCollection;
            }
            set {
                inCollection = value;
                if (value)
                    dataSet = Table.DataSet;
                else
                    dataSet = null;
            }
        }

        /// <devdoc>
        /// <para>Gets the <see cref='Microsoft.Data.DataTable'/> to which the constraint applies.</para>
        /// </devdoc>
        [ResDescriptionAttribute(ResHelper.ResourceNames.ConstraintTableDescr)]
        public abstract DataTable Table {
            get;
        }

        /// <devdoc>
        ///    <para>Gets the collection of customized user information.</para>
        /// </devdoc>
        [
        ResCategoryAttribute(ResHelper.ResourceNames.DataCategory_Data), 
        Browsable(false),
        ResDescriptionAttribute(ResHelper.ResourceNames.ExtendedPropertiesDescr)
        ]
        public PropertyCollection ExtendedProperties {
            get {
                if (extendedProperties == null) {
                    extendedProperties = new PropertyCollection();
                }
                return extendedProperties;
            }
        }

        internal abstract bool ContainsColumn(DataColumn column);

        internal abstract bool CanEnableConstraint();

        internal abstract Constraint Clone(DataSet destination);
        internal abstract Constraint Clone(DataSet destination, bool ignoreNSforTableLookup);

        internal void CheckConstraint() {
            if (!CanEnableConstraint()) {
                throw ExceptionBuilder.ConstraintViolation(ConstraintName);
            }
        }

        internal abstract void CheckCanAddToCollection(ConstraintCollection constraint);
        internal abstract bool CanBeRemovedFromCollection(ConstraintCollection constraint, bool fThrowException);

        internal abstract void CheckConstraint(DataRow row, DataRowAction action);
        internal abstract void CheckState();

         protected void CheckStateForProperty() {
            try {
                CheckState();
            }
            catch (Exception e) {
                // UNDONE - should not be catching all exceptions!!!
                if (!Common.ADP.IsCatchableExceptionType (e)) {
                   throw;
                }
                throw ExceptionBuilder.BadObjectPropertyAccess(e.Message);            
            }
        }

        /// <devdoc>
        /// <para>Gets the <see cref='System.Data.DataSet'/> to which this constraint belongs.</para>
        /// </devdoc>
        [CLSCompliant(false)]
        protected virtual DataSet _DataSet {
            get {
                return dataSet;
            }
        }
        
        /// <devdoc>
        /// <para>Sets the constraint's <see cref='System.Data.DataSet'/>.</para>
        /// </devdoc>
        protected internal void SetDataSet(DataSet dataSet) {
            this.dataSet = dataSet;
        }
        internal abstract bool IsConstraintViolated();

         public override string ToString() {
            return ConstraintName;
        }
    }
}
