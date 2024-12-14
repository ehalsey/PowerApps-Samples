# Dataverse Table Creator

This console application reads a simplified JSON file to create a Dataverse table with custom fields, including local and global picklists, lookup fields, and relationships.

## Prerequisites

- .NET 9.0 SDK or later
- A Dataverse environment
- An application registered in Azure Active Directory with the necessary permissions to access Dataverse

## Configuration

1. Update the `appsettings.json` file with your Dataverse connection details.

```json
{
  "ConnectionStrings": {
    "default": "AuthType=OAuth;Url=https://myorg.crm.dynamics.com;RedirectUri=http://localhost;AppId=51f81489-12ee-4a9e-aaae-a2591f45987d;LoginPrompt=Auto"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Trace"
    }
  }
}
```

## Usage

1. Build the project:

```sh
dotnet build
```

2. Run the application with the path to the JSON file as an argument:

```sh
dotnet run --path/to/your/jsonfile.json
```

## JSON File Format

The JSON file should have the following format:

```json
{
  "TableName": "new_customtable",
  "TableDisplayName": "Custom Table",
  "TableDescription": "A custom table created from JSON",
  "Fields": [
    {
      "FieldName": "new_customfield1",
      "FieldDisplayName": "Custom Field 1",
      "FieldType": "String"
    },
    {
      "FieldName": "new_customfield2",
      "FieldDisplayName": "Custom Field 2",
      "FieldType": "Picklist"
    }
  ]
}
```

## Example

Here is an example of a JSON file that creates a custom table with two fields:

```json
{
  "TableName": "new_exampletable",
  "TableDisplayName": "Example Table",
  "TableDescription": "An example table created from JSON",
  "Fields": [
    {
      "FieldName": "new_examplefield1",
      "FieldDisplayName": "Example Field 1",
      "FieldType": "String"
    },
    {
      "FieldName": "new_examplefield2",
      "FieldDisplayName": "Example Field 2",
      "FieldType": "Picklist"
    }
  ]
}
```

## License

This project is licensed under the MIT License.
