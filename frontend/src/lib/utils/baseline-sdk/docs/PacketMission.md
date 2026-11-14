
# PacketMission


## Properties

Name | Type
------------ | -------------
`name` | string
`environment` | number
`oneWayLatencySeconds` | [HealthMetricPlausibleMin](HealthMetricPlausibleMin.md)
`oneWayLatencyText` | string
`linkUp` | boolean
`missionDay` | [ApiCrewCrewIdMeasurementsGetDaysParameter](ApiCrewCrewIdMeasurementsGetDaysParameter.md)
`inAdaptationWindow` | boolean

## Example

```typescript
import type { PacketMission } from ''

// TODO: Update the object below with actual values
const example = {
  "name": null,
  "environment": null,
  "oneWayLatencySeconds": null,
  "oneWayLatencyText": null,
  "linkUp": null,
  "missionDay": null,
  "inAdaptationWindow": null,
} satisfies PacketMission

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as PacketMission
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


