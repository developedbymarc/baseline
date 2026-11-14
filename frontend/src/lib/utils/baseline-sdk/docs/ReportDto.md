
# ReportDto


## Properties

Name | Type
------------ | -------------
`scope` | number
`window` | number
`period` | [ApiCrewCrewIdMeasurementsGetDaysParameter](ApiCrewCrewIdMeasurementsGetDaysParameter.md)
`periodStart` | Date
`periodEnd` | Date
`startDay` | [ApiCrewCrewIdMeasurementsGetDaysParameter](ApiCrewCrewIdMeasurementsGetDaysParameter.md)
`endDay` | [ApiCrewCrewIdMeasurementsGetDaysParameter](ApiCrewCrewIdMeasurementsGetDaysParameter.md)
`complete` | boolean
`view` | string
`dataAsOf` | Date
`dataAsOfMet` | string
`adaptationWindow` | [AdaptationInfo](AdaptationInfo.md)
`parameters` | [ReportOptions](ReportOptions.md)
`members` | [Array&lt;MemberReport&gt;](MemberReport.md)
`alertsTotal` | [AlertCounts](AlertCounts.md)
`highlights` | Array&lt;string&gt;

## Example

```typescript
import type { ReportDto } from ''

// TODO: Update the object below with actual values
const example = {
  "scope": null,
  "window": null,
  "period": null,
  "periodStart": null,
  "periodEnd": null,
  "startDay": null,
  "endDay": null,
  "complete": null,
  "view": null,
  "dataAsOf": null,
  "dataAsOfMet": null,
  "adaptationWindow": null,
  "parameters": null,
  "members": null,
  "alertsTotal": null,
  "highlights": null,
} satisfies ReportDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as ReportDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


