
# ContextFactor


## Properties

Name | Type
------------ | -------------
`ruleId` | string
`message` | string
`severityModifier` | [ApiCrewCrewIdMeasurementsGetDaysParameter](ApiCrewCrewIdMeasurementsGetDaysParameter.md)
`sources` | [Array&lt;ContextFactorSource&gt;](ContextFactorSource.md)

## Example

```typescript
import type { ContextFactor } from ''

// TODO: Update the object below with actual values
const example = {
  "ruleId": null,
  "message": null,
  "severityModifier": null,
  "sources": null,
} satisfies ContextFactor

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as ContextFactor
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


