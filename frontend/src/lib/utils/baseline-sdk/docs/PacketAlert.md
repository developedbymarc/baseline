
# PacketAlert


## Properties

Name | Type
------------ | -------------
`id` | string
`level` | number
`reason` | string
`ruleId` | string
`at` | Date
`atMet` | string
`metricCode` | string
`metricName` | string
`value` | [ClockRequestRate](ClockRequestRate.md)
`unit` | string

## Example

```typescript
import type { PacketAlert } from ''

// TODO: Update the object below with actual values
const example = {
  "id": null,
  "level": null,
  "reason": null,
  "ruleId": null,
  "at": null,
  "atMet": null,
  "metricCode": null,
  "metricName": null,
  "value": null,
  "unit": null,
} satisfies PacketAlert

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as PacketAlert
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


