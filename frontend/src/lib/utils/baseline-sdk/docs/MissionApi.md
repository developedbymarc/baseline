# MissionApi

All URIs are relative to *http://localhost:5074*

| Method | HTTP request | Description |
|------------- | ------------- | -------------|
| [**apiMissionGet**](MissionApi.md#apimissionget) | **GET** /api/mission |  |
| [**apiMissionStartPost**](MissionApi.md#apimissionstartpost) | **POST** /api/mission/start |  |



## apiMissionGet

> ApiResponseOfMissionStateDto apiMissionGet()



### Example

```ts
import {
  Configuration,
  MissionApi,
} from '';
import type { ApiMissionGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new MissionApi();

  try {
    const data = await api.apiMissionGet();
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

[**ApiResponseOfMissionStateDto**](ApiResponseOfMissionStateDto.md)

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


## apiMissionStartPost

> ApiResponseOfObject apiMissionStartPost()



### Example

```ts
import {
  Configuration,
  MissionApi,
} from '';
import type { ApiMissionStartPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new MissionApi();

  try {
    const data = await api.apiMissionStartPost();
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

[**ApiResponseOfObject**](ApiResponseOfObject.md)

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

