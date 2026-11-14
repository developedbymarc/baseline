
# ProtocolDefinition


## Properties

Name | Type
------------ | -------------
`id` | string
`title` | string
`version` | string
`language` | string
`start` | string
`disclaimer` | string
`trigger` | [ProtocolTrigger](ProtocolTrigger.md)
`latencyRule` | string
`treeYaml` | [Array&lt;ProtocolNode&gt;](ProtocolNode.md)
`sources` | [Array&lt;ProtocolSource&gt;](ProtocolSource.md)

## Example

```typescript
import type { ProtocolDefinition } from ''

// TODO: Update the object below with actual values
const example = {
  "id": null,
  "title": null,
  "version": null,
  "language": null,
  "start": null,
  "disclaimer": null,
  "trigger": null,
  "latencyRule": null,
  "treeYaml": null,
  "sources": null,
} satisfies ProtocolDefinition

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as ProtocolDefinition
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


