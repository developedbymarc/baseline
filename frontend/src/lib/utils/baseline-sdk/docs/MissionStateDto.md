
# MissionStateDto


## Properties

Name | Type
------------ | -------------
`id` | string
`name` | string
`environment` | number
`launchDate` | Date
`mode` | number
`missionNow` | Date
`metSeconds` | [HealthMetricPlausibleMin](HealthMetricPlausibleMin.md)
`met` | string
`clockRate` | [HealthMetricPlausibleMin](HealthMetricPlausibleMin.md)
`clockPaused` | boolean
`oneWayLatencySeconds` | [HealthMetricPlausibleMin](HealthMetricPlausibleMin.md)
`linkUp` | boolean
`onboardFirst` | boolean
`onboardFirstThresholdSeconds` | [HealthMetricPlausibleMin](HealthMetricPlausibleMin.md)

## Example

```typescript
import type { MissionStateDto } from ''

// TODO: Update the object below with actual values
const example = {
  "id": null,
  "name": null,
  "environment": null,
  "launchDate": null,
  "mode": null,
  "missionNow": null,
  "metSeconds": null,
  "met": null,
  "clockRate": null,
  "clockPaused": null,
  "oneWayLatencySeconds": null,
  "linkUp": null,
  "onboardFirst": null,
  "onboardFirstThresholdSeconds": null,
} satisfies MissionStateDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as MissionStateDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


