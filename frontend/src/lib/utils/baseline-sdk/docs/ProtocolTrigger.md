
# ProtocolTrigger


## Properties

Name | Type
------------ | -------------
`symptoms` | Array&lt;string&gt;
`redMetrics` | Array&lt;string&gt;
`requiresSymptom` | boolean
`windowMin` | [MetricRowBaselineN](MetricRowBaselineN.md)

## Example

```typescript
import type { ProtocolTrigger } from ''

// TODO: Update the object below with actual values
const example = {
  "symptoms": null,
  "redMetrics": null,
  "requiresSymptom": null,
  "windowMin": null,
} satisfies ProtocolTrigger

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as ProtocolTrigger
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


