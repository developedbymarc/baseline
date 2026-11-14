
# ReportOptions


## Properties

Name | Type
------------ | -------------
`trendMaxPValue` | [HealthMetricPlausibleMin](HealthMetricPlausibleMin.md)
`trendMinEffectSd` | [HealthMetricPlausibleMin](HealthMetricPlausibleMin.md)
`minPointsForConfidence` | [ApiCrewCrewIdMeasurementsGetDaysParameter](ApiCrewCrewIdMeasurementsGetDaysParameter.md)
`adaptationWindowDaysFallback` | [ApiCrewCrewIdMeasurementsGetDaysParameter](ApiCrewCrewIdMeasurementsGetDaysParameter.md)

## Example

```typescript
import type { ReportOptions } from ''

// TODO: Update the object below with actual values
const example = {
  "trendMaxPValue": null,
  "trendMinEffectSd": null,
  "minPointsForConfidence": null,
  "adaptationWindowDaysFallback": null,
} satisfies ReportOptions

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as ReportOptions
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


