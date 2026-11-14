
# PacketProtocol


## Properties

Name | Type
------------ | -------------
`runId` | string
`protocolId` | string
`title` | string
`version` | string
`startedAt` | Date
`startedMet` | string
`endedAt` | Date
`steps` | [Array&lt;PacketStep&gt;](PacketStep.md)

## Example

```typescript
import type { PacketProtocol } from ''

// TODO: Update the object below with actual values
const example = {
  "runId": null,
  "protocolId": null,
  "title": null,
  "version": null,
  "startedAt": null,
  "startedMet": null,
  "endedAt": null,
  "steps": null,
} satisfies PacketProtocol

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as PacketProtocol
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


