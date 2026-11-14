
# CrewMemberDto


## Properties

Name | Type
------------ | -------------
`id` | string
`name` | string
`role` | string
`age` | [ApiCrewCrewIdMeasurementsGetDaysParameter](ApiCrewCrewIdMeasurementsGetDaysParameter.md)
`sex` | string
`heightCm` | [ApiCrewCrewIdMeasurementsGetDaysParameter](ApiCrewCrewIdMeasurementsGetDaysParameter.md)
`notes` | string

## Example

```typescript
import type { CrewMemberDto } from ''

// TODO: Update the object below with actual values
const example = {
  "id": null,
  "name": null,
  "role": null,
  "age": null,
  "sex": null,
  "heightCm": null,
  "notes": null,
} satisfies CrewMemberDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as CrewMemberDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


