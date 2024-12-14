using Microsoft.Extensions.Configuration;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Newtonsoft.Json.Linq;
using System;

namespace DataverseTableCreator
{
    class Program
    {
        static IConfiguration Configuration { get; }

        static Program()
        {
            string? path = Environment.GetEnvironmentVariable("DATAVERSE_APPSETTINGS");
            if (path == null) path = "appsettings.json";

            Configuration = new ConfigurationBuilder()
                .AddJsonFile(path, optional: false, reloadOnChange: true)
                .Build();
        }

        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Please provide the path to the JSON file.");
                return;
            }

            string jsonFilePath = args[0];
            string jsonContent = System.IO.File.ReadAllText(jsonFilePath);
            JObject tableDefinition = JObject.Parse(jsonContent);

            ServiceClient serviceClient = new(Configuration.GetConnectionString("default"));

            CreateTable(serviceClient, tableDefinition);
        }

        static void CreateTable(ServiceClient serviceClient, JObject tableDefinition)
        {
            string tableName = tableDefinition["TableName"]?.ToString() ?? throw new ArgumentNullException("TableName");
            string tableDisplayName = tableDefinition["TableDisplayName"]?.ToString() ?? throw new ArgumentNullException("TableDisplayName");
            string tableDisplayCollectionName = tableDefinition["TableDisplayCollectionName"]?.ToString() ?? throw new ArgumentNullException("TableDisplayCollectionName");
            string tableDescription = tableDefinition["TableDescription"]?.ToString() ?? throw new ArgumentNullException("TableDescription");

            var createEntityRequest = new CreateEntityRequest
            {
                Entity = new EntityMetadata
                {
                    SchemaName = tableName,
                    DisplayName = new Label(tableDisplayName, 1033), // Singular name
                    DisplayCollectionName = new Label(tableDisplayCollectionName, 1033), // Plural name
                    Description = new Label(tableDescription, 1033),
                    OwnershipType = OwnershipTypes.UserOwned
                },
                PrimaryAttribute = new StringAttributeMetadata
                {
                    SchemaName = "new_name",
                    RequiredLevel = new AttributeRequiredLevelManagedProperty(AttributeRequiredLevel.None),
                    MaxLength = 100,
                    Format = StringFormat.Text,
                    DisplayName = new Label("Name", 1033),
                    Description = new Label("The primary attribute for the table.", 1033)
                }
            };

            serviceClient.Execute(createEntityRequest);

            JArray fields = (JArray)(tableDefinition["Fields"] ?? new JArray());
            foreach (JObject field in fields)
            {
                CreateField(serviceClient, tableName, field);
            }

            JArray relationships = (JArray)(tableDefinition["Relationships"] ?? new JArray());
            foreach (JObject relationship in relationships)
            {
                CreateRelationship(serviceClient, tableName, relationship);
            }
        }

        static void CreateField(ServiceClient serviceClient, string tableName, JObject fieldDefinition)
        {
            string fieldName = fieldDefinition["FieldName"]?.ToString() ?? throw new ArgumentNullException("FieldName");
            string fieldDisplayName = fieldDefinition["FieldDisplayName"]?.ToString() ?? throw new ArgumentNullException("FieldDisplayName");
            string fieldType = fieldDefinition["FieldType"]?.ToString() ?? throw new ArgumentNullException("FieldType");

            AttributeMetadata attributeMetadata = fieldType switch
            {
                "String" => new StringAttributeMetadata
                {
                    SchemaName = fieldName,
                    DisplayName = new Label(fieldDisplayName, 1033),
                    MaxLength = 100
                },
                "Picklist" => new PicklistAttributeMetadata
                {
                    SchemaName = fieldName,
                    DisplayName = new Label(fieldDisplayName, 1033),
                    OptionSet = new OptionSetMetadata
                    {
                        IsGlobal = false,
                        OptionSetType = OptionSetType.Picklist
                    }
                },
                "Lookup" => new LookupAttributeMetadata
                {
                    SchemaName = fieldName,
                    DisplayName = new Label(fieldDisplayName, 1033),
                    Targets = new string[] { fieldDefinition["TargetEntity"]?.ToString() ?? throw new ArgumentNullException("TargetEntity") }
                },
                _ => throw new NotSupportedException($"Field type '{fieldType}' is not supported.")
            };

            if (fieldType == "Picklist")
            {
                JArray options = (JArray)(fieldDefinition["Options"] ?? new JArray());
                foreach (JObject option in options)
                {
                    string optionLabel = option["Label"]?.ToString() ?? throw new ArgumentNullException("Label");
                    int optionValue = option["Value"]?.ToObject<int>() ?? throw new ArgumentNullException("Value");
                    ((PicklistAttributeMetadata)attributeMetadata).OptionSet.Options.Add(new OptionMetadata(new Label(optionLabel, 1033), optionValue));
                }
            }

            var createAttributeRequest = new CreateAttributeRequest
            {
                EntityName = tableName,
                Attribute = attributeMetadata
            };

            serviceClient.Execute(createAttributeRequest);
        }

        static void CreateRelationship(ServiceClient serviceClient, string tableName, JObject relationshipDefinition)
        {
            // Extract properties from the JSON definition
            string relationshipName = relationshipDefinition["RelationshipName"]?.ToString() ?? throw new ArgumentNullException("RelationshipName");
            string relatedTableName = relationshipDefinition["RelatedTableName"]?.ToString() ?? throw new ArgumentNullException("RelatedTableName");
            string relationshipType = relationshipDefinition["RelationshipType"]?.ToString() ?? throw new ArgumentNullException("RelationshipType");

            switch (relationshipType)
            {
                case "OneToMany":
                    // Define the One-to-Many relationship
                    var oneToManyRelationshipRequest = new CreateOneToManyRequest
                    {
                        OneToManyRelationship = new OneToManyRelationshipMetadata
                        {
                            SchemaName = relationshipName,
                            ReferencedEntity = tableName,
                            ReferencingEntity = relatedTableName,
                            AssociatedMenuConfiguration = new AssociatedMenuConfiguration
                            {
                                Behavior = AssociatedMenuBehavior.UseLabel,
                                Group = AssociatedMenuGroup.Details,
                                Label = new Label("Related " + tableName, 1033),
                                Order = 10000
                            },
                            CascadeConfiguration = new CascadeConfiguration
                            {
                                Assign = CascadeType.NoCascade,
                                Delete = CascadeType.RemoveLink,
                                Merge = CascadeType.NoCascade,
                                Reparent = CascadeType.NoCascade,
                                Share = CascadeType.NoCascade,
                                Unshare = CascadeType.NoCascade
                            }
                        },
                        Lookup = new LookupAttributeMetadata
                        {
                            SchemaName = "new_" + tableName.ToLower() + "_id",
                            DisplayName = new Label(tableName + " Lookup", 1033),
                            RequiredLevel = new AttributeRequiredLevelManagedProperty(AttributeRequiredLevel.None),
                            Description = new Label("Lookup for " + tableName, 1033)
                        }
                    };

                    // Execute the request
                    serviceClient.Execute(oneToManyRelationshipRequest);
                    Console.WriteLine($"The one-to-many relationship '{relationshipName}' has been created between '{tableName}' and '{relatedTableName}'.");
                    break;

                case "ManyToMany":
                    // Define the Many-to-Many relationship
                    var manyToManyRelationshipRequest = new CreateManyToManyRequest
                    {
                        IntersectEntitySchemaName = relationshipName,
                        ManyToManyRelationship = new ManyToManyRelationshipMetadata
                        {
                            SchemaName = relationshipName,
                            Entity1LogicalName = tableName,
                            Entity2LogicalName = relatedTableName,
                            Entity1AssociatedMenuConfiguration = new AssociatedMenuConfiguration
                            {
                                Behavior = AssociatedMenuBehavior.UseLabel,
                                Group = AssociatedMenuGroup.Details,
                                Label = new Label("Related " + tableName, 1033),
                                Order = 10000
                            },
                            Entity2AssociatedMenuConfiguration = new AssociatedMenuConfiguration
                            {
                                Behavior = AssociatedMenuBehavior.UseLabel,
                                Group = AssociatedMenuGroup.Details,
                                Label = new Label("Related " + relatedTableName, 1033),
                                Order = 10000
                            }
                        }
                    };

                    // Execute the request
                    serviceClient.Execute(manyToManyRelationshipRequest);
                    Console.WriteLine($"The many-to-many relationship '{relationshipName}' has been created between '{tableName}' and '{relatedTableName}'.");
                    break;

                default:
                    throw new NotSupportedException($"Relationship type '{relationshipType}' is not supported.");
            }
        }
    }
}
