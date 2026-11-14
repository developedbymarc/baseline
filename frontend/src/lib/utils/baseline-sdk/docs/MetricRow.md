
# MetricRow


## Properties

Name | Type
------------ | -------------
`metricCode` | string
`metricName` | string
`unit` | string
`windowN` | [ApiCrewCrewIdMeasurementsGetDaysParameter](ApiCrewCrewIdMeasurementsGetDaysParameter.md)
`windowMean` | [ClockRequestRate](ClockRequestRate.md)
`baselineMean` | [ClockRequestRate](ClockRequestRate.md)
`baselineN` | [MetricRowBaselineN](MetricRowBaselineN.md)
`sdEffective` | [ClockRequestRate](ClockRequestRate.md)
`baselineShrunk` | boolean
`baselineSource` | string
`delta` | [ClockRequestRate](ClockRequestRate.md)
`zSd` | [ClockRequestRate](ClockRequestRate.md)
`zShift` | [ClockRequestRate](ClockRequestRate.md)
`trendSlopePerDay` | [ClockRequestRate](ClockRequestRate.md)
`trendChangeOverWindow` | [ClockRequestRate](ClockRequestRate.md)
`trendPValue` | [ClockRequestRate](ClockRequestRate.md)
`trend` | string
`missed` | [ApiCrewCrewIdMeasurementsGetDaysParameter](ApiCrewCrewIdMeasurementsGetDaysParameter.md)
`expected` | [ApiCrewCrewIdMeasurementsGetDaysParameter](ApiCrewCrewIdMeasurementsGetDaysParameter.md)
`lowConfidence` | boolean

## Example

```typescript
import type { MetricRow } from ''

// TODO: Update the object below with actual values
const example = {
  "metricCode": null,
  "metricName": null,
  "unit": null,
  "windowN": null,
  "windowMean": null,
  "baselineMean": null,
  "baselineN": null,
  "sdEffective": null,
  "baselineShrunk": null,
  "baselineSource": null,
  "delta": null,
  "zSd": null,
  "zShift": null,
  "trendSlopePerDay": null,
  "trendChangeOverWindow": null,
  "trendPValue": null,
  "trend": null,
  "missed": null,
  "expected": null,
  "lowConfidence": null,
} satisfies MetricRow

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as MetricRow
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


