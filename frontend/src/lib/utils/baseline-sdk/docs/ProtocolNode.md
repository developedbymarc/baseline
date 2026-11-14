
# ProtocolNode


## Properties

Name | Type
------------ | -------------
`id` | string
`type` | string
`text` | string
`options` | [Array&lt;ProtocolOption&gt;](ProtocolOption.md)
`next` | string
`metrics` | Array&lt;string&gt;
`src` | Array&lt;string&gt;
`outcome` | string
`logAnswer` | boolean
`log` | boolean
`timerS` | [MetricRowBaselineN](MetricRowBaselineN.md)
`buildPacket` | boolean
`notifyGround` | boolean
`review` | boolean
`note` | string

## Example

```typescript
import type { ProtocolNode } from ''

// TODO: Update the object below with actual values
const example = {
  "id": null,
  "type": null,
  "text": null,
  "options": null,
  "next": null,
  "metrics": null,
  "src": null,
  "outcome": null,
  "logAnswer": null,
  "log": null,
  "timerS": null,
  "buildPacket": null,
  "notifyGround": null,
  "review": null,
  "note": null,
} satisfies ProtocolNode

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as ProtocolNode
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


