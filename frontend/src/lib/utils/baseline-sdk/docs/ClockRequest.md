
# ClockRequest


## Properties

Name | Type
------------ | -------------
`rate` | [ClockRequestRate](ClockRequestRate.md)
`paused` | boolean
`advanceSeconds` | [ClockRequestRate](ClockRequestRate.md)
`fastForward` | boolean

## Example

```typescript
import type { ClockRequest } from ''

// TODO: Update the object below with actual values
const example = {
  "rate": null,
  "paused": null,
  "advanceSeconds": null,
  "fastForward": null,
} satisfies ClockRequest

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as ClockRequest
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


