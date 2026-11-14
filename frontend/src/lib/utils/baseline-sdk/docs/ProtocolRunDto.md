
# ProtocolRunDto


## Properties

Name | Type
------------ | -------------
`id` | string
`crewMemberId` | string
`protocolId` | string
`protocolVersion` | string
`alertId` | string
`startedAt` | Date
`endedAt` | Date
`steps` | any
`packetDraft` | [HandoffPacket](HandoffPacket.md)

## Example

```typescript
import type { ProtocolRunDto } from ''

// TODO: Update the object below with actual values
const example = {
  "id": null,
  "crewMemberId": null,
  "protocolId": null,
  "protocolVersion": null,
  "alertId": null,
  "startedAt": null,
  "endedAt": null,
  "steps": null,
  "packetDraft": null,
} satisfies ProtocolRunDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as ProtocolRunDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


