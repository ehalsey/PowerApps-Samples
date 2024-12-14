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
            string tableName = tableDefinition["TableName"].ToString();
            string tableDisplayName = tableDefinition["TableDisplayName"].ToString();
            string tableDescription = tableDefinition["TableDescription"].ToString();

            var createEntityRequest = new CreateEntityRequest
            {
                Entity = new EntityMetadata
                {
                    SchemaName = tableName,
                    DisplayName = new Label(tableDisplayName, 1033),
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

            JArray fields = (JArray)tableDefinition["Fields"];
            foreach (JObject field in fields)
            {
                CreateField(serviceClient, tableName, field);
            }

            JArray relationships = (JArray)tableDefinition["Relationships"];
            foreach (JObject relationship in relationships)
            {
                CreateRelationship(serviceClient, tableName, relationship);
            }
        }

        static void CreateField(ServiceClient serviceClient, string tableName, JObject fieldDefinition)
        {
            string fieldName = fieldDefinition["FieldName"].ToString();
            string fieldDisplayName = fieldDefinition["FieldDisplayName"].ToString();
            string fieldType = fieldDefinition["FieldType"].ToString();

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
                        OptionSetType = OptionSetType.Picklist,
                        Options = new OptionMetadataCollection()
                    }
                },
                "Lookup" => new LookupAttributeMetadata
                {
                    SchemaName = fieldName,
                    DisplayName = new Label(fieldDisplayName, 1033),
                    Targets = new string[] { fieldDefinition["TargetEntity"].ToString() }
                },
                _ => throw new NotSupportedException($"Field type '{fieldType}' is not supported.")
            };

            if (fieldType == "Picklist")
            {
                JArray options = (JArray)fieldDefinition["Options"];
                foreach (JObject option in options)
                {
                    string optionLabel = option["Label"].ToString();
                    int optionValue = (int)option["Value"];
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
            string relationshipName = relationshipDefinition["RelationshipName"].ToString();
            string relatedTableName = relationshipDefinition["RelatedTableName"].ToString();
            string relationshipType = relationshipDefinition["RelationshipType"].ToString();

            RelationshipMetadataBase relationshipMetadata = relationshipType switch
            {
                "OneToMany" => new OneToManyRelationshipMetadata
                {
                    SchemaName = relationshipName,
                    ReferencedEntity = tableName,
                    ReferencingEntity = relatedTableName,
                    AssociatedMenuConfiguration = new AssociatedMenuConfiguration
                    {
                        Behavior = AssociatedMenuBehavior.UseCollectionName,
                        Group = AssociatedMenuGroup.Details,
                        Label = new Label(relationshipName, 1033),
                        Order = 10000
                    }
                },
                "ManyToMany" => new ManyToManyRelationshipMetadata
                {
                    SchemaName = relationshipName,
                    Entity1LogicalName = tableName,
                    Entity2LogicalName = relatedTableName,
                    Entity1AssociatedMenuConfiguration = new AssociatedMenuConfiguration
                    {
                        Behavior = AssociatedMenuBehavior.UseCollectionName,
                        Group = AssociatedMenuGroup.Details,
                        Label = new Label(relationshipName, 1033),
                        Order = 10000
                    },
                    Entity2AssociatedMenuConfiguration = new AssociatedMenuConfiguration
                    {
                        Behavior = AssociatedMenuBehavior.UseCollectionName,
                        Group = AssociatedMenuGroup.Details,
                        Label = new Label(relationshipName, 1033),
                        Order = 10000
                    }
                },
                _ => throw new NotSupportedException($"Relationship type '{relationshipType}' is not supported.")
            };

            var createRelationshipRequest = new CreateRelationshipRequest
            {
                EntityName = tableName,
                Relationship = relationshipMetadata
            };

            serviceClient.Execute(createRelationshipRequest);
        }
    }
}
