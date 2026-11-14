
# AlertDto


## Properties

Name | Type
------------ | -------------
`id` | string
`crewMemberId` | string
`measurementId` | string
`level` | number
`reason` | string
`ruleId` | string
`createdAt` | Date
`acknowledgedBy` | string
`context` | [](.md)

## Example

```typescript
import type { AlertDto } from ''

// TODO: Update the object below with actual values
const example = {
  "id": null,
  "crewMemberId": null,
  "measurementId": null,
  "level": null,
  "reason": null,
  "ruleId": null,
  "createdAt": null,
  "acknowledgedBy": null,
  "context": null,
} satisfies AlertDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as AlertDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


