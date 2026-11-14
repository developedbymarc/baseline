# LinkApi

All URIs are relative to *http://localhost:5074*

| Method | HTTP request | Description |
|------------- | ------------- | -------------|
| [**apiLinkQueueGet**](LinkApi.md#apilinkqueueget) | **GET** /api/link/queue |  |



## apiLinkQueueGet

> ApiResponseOfIEnumerableOfLinkMessageDto apiLinkQueueGet(type)



### Example

```ts
import {
  Configuration,
  LinkApi,
} from '';
import type { ApiLinkQueueGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new LinkApi();

  const body = {
    // string (optional)
    type: type_example,
  } satisfies ApiLinkQueueGetRequest;

  try {
    const data = await api.apiLinkQueueGet(body);
    console.log(data);
  } catch (error) {
    console.error(error);
  }
}

// Run the test
example().catch(console.error);
```

### Parameters


| Name | Type | Description  | Notes |
|------------- | ------------- | ------------- | -------------|
| **type** | `string` |  | [Optional] [Defaults to `undefined`] |

### Return type

[**ApiResponseOfIEnumerableOfLinkMessageDto**](ApiResponseOfIEnumerableOfLinkMessageDto.md)

### Authorization

No authorization required

### HTTP request headers

- **Content-Type**: Not defined
- **Accept**: `text/plain`, `application/json`, `text/json`


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** | OK |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)

