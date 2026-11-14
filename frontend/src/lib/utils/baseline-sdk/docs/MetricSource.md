
# MetricSource


## Properties

Name | Type
------------ | -------------
`fields` | Array&lt;string&gt;
`publisher` | string
`title` | string
`url` | string
`note` | string
`checked` | string
`opened` | boolean

## Example

```typescript
import type { MetricSource } from ''

// TODO: Update the object below with actual values
const example = {
  "fields": null,
  "publisher": null,
  "title": null,
  "url": null,
  "note": null,
  "checked": null,
  "opened": null,
} satisfies MetricSource

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as MetricSource
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


