
# HandoffPacket


## Properties

Name | Type
------------ | -------------
`schema` | string
`createdAt` | Date
`createdMet` | string
`createdUtc` | string
`crewMemberId` | string
`crewName` | string
`alertId` | string
`runId` | string
`note` | string
`data` | [PacketData](PacketData.md)
`text` | string

## Example

```typescript
import type { HandoffPacket } from ''

// TODO: Update the object below with actual values
const example = {
  "schema": null,
  "createdAt": null,
  "createdMet": null,
  "createdUtc": null,
  "crewMemberId": null,
  "crewName": null,
  "alertId": null,
  "runId": null,
  "note": null,
  "data": null,
  "text": null,
} satisfies HandoffPacket

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as HandoffPacket
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


