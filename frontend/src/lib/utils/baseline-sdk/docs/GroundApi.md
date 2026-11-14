# GroundApi

All URIs are relative to *http://localhost:5074*

| Method | HTTP request | Description |
|------------- | ------------- | -------------|
| [**apiGroundInboxGet**](GroundApi.md#apigroundinboxget) | **GET** /api/ground/inbox |  |
| [**apiGroundOverviewGet**](GroundApi.md#apigroundoverviewget) | **GET** /api/ground/overview |  |



## apiGroundInboxGet

> ApiResponseOfIEnumerableOfLinkMessageDto apiGroundInboxGet(type)



### Example

```ts
import {
  Configuration,
  GroundApi,
} from '';
import type { ApiGroundInboxGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new GroundApi();

  const body = {
    // string (optional)
    type: type_example,
  } satisfies ApiGroundInboxGetRequest;

  try {
    const data = await api.apiGroundInboxGet(body);
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


## apiGroundOverviewGet

> ApiResponseOfIEnumerableOfObject apiGroundOverviewGet()



### Example

```ts
import {
  Configuration,
  GroundApi,
} from '';
import type { ApiGroundOverviewGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new GroundApi();

  try {
    const data = await api.apiGroundOverviewGet();
    console.log(data);
  } catch (error) {
    console.error(error);
  }
}

// Run the test
example().catch(console.error);
```

### Parameters

This endpoint does not need any parameter.

### Return type

[**ApiResponseOfIEnumerableOfObject**](ApiResponseOfIEnumerableOfObject.md)

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

