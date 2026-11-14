
# PacketBaseline


## Properties

Name | Type
------------ | -------------
`metricCode` | string
`metricName` | string
`unit` | string
`mean` | [HealthMetricPlausibleMin](HealthMetricPlausibleMin.md)
`sdEffective` | [HealthMetricPlausibleMin](HealthMetricPlausibleMin.md)
`n` | [ApiCrewCrewIdMeasurementsGetDaysParameter](ApiCrewCrewIdMeasurementsGetDaysParameter.md)
`shrunk` | boolean
`latestValue` | [ClockRequestRate](ClockRequestRate.md)
`latestAt` | Date
`latestZ` | [ClockRequestRate](ClockRequestRate.md)
`summary` | string

## Example

```typescript
import type { PacketBaseline } from ''

// TODO: Update the object below with actual values
const example = {
  "metricCode": null,
  "metricName": null,
  "unit": null,
  "mean": null,
  "sdEffective": null,
  "n": null,
  "shrunk": null,
  "latestValue": null,
  "latestAt": null,
  "latestZ": null,
  "summary": null,
} satisfies PacketBaseline

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as PacketBaseline
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


