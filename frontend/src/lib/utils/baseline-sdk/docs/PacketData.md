
# PacketData


## Properties

Name | Type
------------ | -------------
`mission` | [PacketMission](PacketMission.md)
`crew` | [PacketCrew](PacketCrew.md)
`trigger` | [PacketTrigger](PacketTrigger.md)
`measurements72h` | [Array&lt;PacketMeasurement&gt;](PacketMeasurement.md)
`baselines` | [Array&lt;PacketBaseline&gt;](PacketBaseline.md)
`protocol` | [PacketProtocol](PacketProtocol.md)
`contextFactors` | [Array&lt;ContextFactor&gt;](ContextFactor.md)
`contextOutcome` | [PacketContextOutcome](PacketContextOutcome.md)
`followUp` | [Array&lt;PacketFollowUp&gt;](PacketFollowUp.md)
`recommendation` | string

## Example

```typescript
import type { PacketData } from ''

// TODO: Update the object below with actual values
const example = {
  "mission": null,
  "crew": null,
  "trigger": null,
  "measurements72h": null,
  "baselines": null,
  "protocol": null,
  "contextFactors": null,
  "contextOutcome": null,
  "followUp": null,
  "recommendation": null,
} satisfies PacketData

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as PacketData
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


