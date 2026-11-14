
# LinkMessageDto


## Properties

Name | Type
------------ | -------------
`id` | string
`direction` | number
`type` | number
`alertId` | string
`state` | string
`sentAt` | Date
`deliverAt` | Date
`deliveredAt` | Date
`ageSeconds` | [HealthMetricPlausibleMin](HealthMetricPlausibleMin.md)
`ageOfData` | string
`secondsUntilDelivery` | [ClockRequestRate](ClockRequestRate.md)
`payload` | any

## Example

```typescript
import type { LinkMessageDto } from ''

// TODO: Update the object below with actual values
const example = {
  "id": null,
  "direction": null,
  "type": null,
  "alertId": null,
  "state": null,
  "sentAt": null,
  "deliverAt": null,
  "deliveredAt": null,
  "ageSeconds": null,
  "ageOfData": null,
  "secondsUntilDelivery": null,
  "payload": null,
} satisfies LinkMessageDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as LinkMessageDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


