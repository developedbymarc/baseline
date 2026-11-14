
# MemberReport


## Properties

Name | Type
------------ | -------------
`crewMemberId` | string
`name` | string
`role` | string
`rows` | [Array&lt;MetricRow&gt;](MetricRow.md)
`missedTotal` | [ApiCrewCrewIdMeasurementsGetDaysParameter](ApiCrewCrewIdMeasurementsGetDaysParameter.md)
`alerts` | [AlertCounts](AlertCounts.md)
`highlights` | Array&lt;string&gt;

## Example

```typescript
import type { MemberReport } from ''

// TODO: Update the object below with actual values
const example = {
  "crewMemberId": null,
  "name": null,
  "role": null,
  "rows": null,
  "missedTotal": null,
  "alerts": null,
  "highlights": null,
} satisfies MemberReport

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as MemberReport
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


