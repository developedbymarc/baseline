
# PacketContextOutcome


## Properties

Name | Type
------------ | -------------
`originalLevel` | number
`level` | number
`downgraded` | boolean
`downgradedBy` | Array&lt;string&gt;

## Example

```typescript
import type { PacketContextOutcome } from ''

// TODO: Update the object below with actual values
const example = {
  "originalLevel": null,
  "level": null,
  "downgraded": null,
  "downgradedBy": null,
} satisfies PacketContextOutcome

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as PacketContextOutcome
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


