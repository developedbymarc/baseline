
# HealthMetric


## Properties

Name | Type
------------ | -------------
`code` | string
`name` | string
`unit` | string
`plausibleMin` | [HealthMetricPlausibleMin](HealthMetricPlausibleMin.md)
`plausibleMax` | [HealthMetricPlausibleMin](HealthMetricPlausibleMin.md)
`normalLow` | [ClockRequestRate](ClockRequestRate.md)
`normalHigh` | [ClockRequestRate](ClockRequestRate.md)
`warnLow` | [ClockRequestRate](ClockRequestRate.md)
`warnHigh` | [ClockRequestRate](ClockRequestRate.md)
`critLow` | [ClockRequestRate](ClockRequestRate.md)
`critHigh` | [ClockRequestRate](ClockRequestRate.md)
`zWarn` | [ClockRequestRate](ClockRequestRate.md)
`zCrit` | [ClockRequestRate](ClockRequestRate.md)
`polarity` | string
`sdPop` | [ClockRequestRate](ClockRequestRate.md)
`instructionsMd` | string
`sources` | [Array&lt;MetricSource&gt;](MetricSource.md)
`hasStatus` | boolean

## Example

```typescript
import type { HealthMetric } from ''

// TODO: Update the object below with actual values
const example = {
  "code": null,
  "name": null,
  "unit": null,
  "plausibleMin": null,
  "plausibleMax": null,
  "normalLow": null,
  "normalHigh": null,
  "warnLow": null,
  "warnHigh": null,
  "critLow": null,
  "critHigh": null,
  "zWarn": null,
  "zCrit": null,
  "polarity": null,
  "sdPop": null,
  "instructionsMd": null,
  "sources": null,
  "hasStatus": null,
} satisfies HealthMetric

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as HealthMetric
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


