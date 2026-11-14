
# PacketTrigger


## Properties

Name | Type
------------ | -------------
`kind` | string
`summary` | string
`alert` | [PacketAlert](PacketAlert.md)
`symptoms` | [Array&lt;PacketSymptom&gt;](PacketSymptom.md)

## Example

```typescript
import type { PacketTrigger } from ''

// TODO: Update the object below with actual values
const example = {
  "kind": null,
  "summary": null,
  "alert": null,
  "symptoms": null,
} satisfies PacketTrigger

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as PacketTrigger
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


