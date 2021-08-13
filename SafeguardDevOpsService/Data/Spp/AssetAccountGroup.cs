using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace OneIdentity.DevOps.Data.Spp
{
#pragma warning disable 1591
    public enum ConditionJoinType
    {
        And,
        Or
    }

    public enum TaggingGroupingObjectAttributes
    {
        Name = 1
    }

    public enum ComparisonOperator
    {
        StartsWith
    }

    /// <summary>
    /// Represents a group of accounts on the appliance.
    /// </summary>
    public class AssetAccountGroup
    {
        /// <summary>Id of the table entry</summary>
        public int Id { get; set; }

        /// <summary>
        /// The name of the account group
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Description regarding the account group
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Whether or not this is a dynamic account group
        /// </summary>
        public bool IsDynamic { get; set; }

        /// <summary>
        /// Date this entity was created (Read-only)
        /// </summary>
        public DateTimeOffset CreatedDate { get; set; }

        /// <summary>
        /// The database ID of the user that created this entity (Read-only)
        /// </summary>
        public int CreatedByUserId { get; set; }

        /// <summary>
        /// The display name of the user that created this entity (Read-only)
        /// </summary>
        public string CreatedByUserDisplayName { get; set; }

        public TaggingGroupingRule GroupingRule { get; set; }
    }

    public class TaggingGroupingRule
    {
        /// <summary>
        /// Description of the rule
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// If true, entities will be evaluated against this rule
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Top level group of conditions
        /// </summary>
        
        public TaggingGroupingConditionGroup RuleConditionGroup { get; set; }
    }

    public class TaggingGroupingConditionGroup
    {
        [IgnoreDataMember]
        public int Id { get; set; }

        /// <summary>
        /// Determines whether the items in this group are ANDed or ORed together
        /// </summary>
        public ConditionJoinType LogicalJoinType { get; set; }

        /// <summary>
        /// The children of this group.
        /// </summary>
        public IEnumerable<TaggingGroupingConditionOrConditionGroup> Children { get; set; }
    }

    public class TaggingGroupingConditionOrConditionGroup
    {
        /// <summary>
        /// A condition to be evaluated. Must belong to a ConditionGroup. Must be null if this is a ConditionGroup
        /// </summary>
        public TaggingGroupingCondition TaggingGroupingCondition { get; set; }

        /// <summary>
        /// A ConditionGroup is a container that contains conditions and/or ConditionGroups. 
        /// </summary>
        public TaggingGroupingConditionGroup TaggingGroupingConditionGroup { get; set; }
    }

    public class TaggingGroupingCondition
    {
        /// <summary>
        /// Which asset or account attribute is being examined.
        /// </summary>
        public TaggingGroupingObjectAttributes ObjectAttribute { get; set; }

        /// <summary>
        /// Indicates how the attribute value should be compared to the CompareValue. Data type dependent.
        /// </summary>
        public ComparisonOperator CompareType { get; set; }

        /// <summary>
        /// The value to compare the ObjectAttribute value against. Always stored/transferred as a string, converted as needed.
        /// </summary>
        public string CompareValue { get; set; }
    }

}
