# DemoApi

All URIs are relative to *http://localhost:5074*

| Method | HTTP request | Description |
|------------- | ------------- | -------------|
| [**apiDemoClockPost**](DemoApi.md#apidemoclockpost) | **POST** /api/demo/clock |  |
| [**apiDemoLinkPost**](DemoApi.md#apidemolinkpost) | **POST** /api/demo/link |  |
| [**apiDemoReseedPost**](DemoApi.md#apidemoreseedpost) | **POST** /api/demo/reseed |  |
| [**apiDemoScene5Post**](DemoApi.md#apidemoscene5post) | **POST** /api/demo/scene5 |  |



## apiDemoClockPost

> ApiResponseOfMissionStateDto apiDemoClockPost(clockRequest)



### Example

```ts
import {
  Configuration,
  DemoApi,
} from '';
import type { ApiDemoClockPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new DemoApi();

  const body = {
    // ClockRequest
    clockRequest: ...,
  } satisfies ApiDemoClockPostRequest;

  try {
    const data = await api.apiDemoClockPost(body);
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
| **clockRequest** | [ClockRequest](ClockRequest.md) |  | |

### Return type

[**ApiResponseOfMissionStateDto**](ApiResponseOfMissionStateDto.md)

### Authorization

No authorization required

### HTTP request headers

- **Content-Type**: `application/json`, `text/json`, `application/*+json`
- **Accept**: `text/plain`, `application/json`, `text/json`


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** | OK |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


## apiDemoLinkPost

> ApiResponseOfMissionStateDto apiDemoLinkPost(linkRequest)



### Example

```ts
import {
  Configuration,
  DemoApi,
} from '';
import type { ApiDemoLinkPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new DemoApi();

  const body = {
    // LinkRequest
    linkRequest: ...,
  } satisfies ApiDemoLinkPostRequest;

  try {
    const data = await api.apiDemoLinkPost(body);
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
| **linkRequest** | [LinkRequest](LinkRequest.md) |  | |

### Return type

[**ApiResponseOfMissionStateDto**](ApiResponseOfMissionStateDto.md)

### Authorization

No authorization required

### HTTP request headers

- **Content-Type**: `application/json`, `text/json`, `application/*+json`
- **Accept**: `text/plain`, `application/json`, `text/json`


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** | OK |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


## apiDemoReseedPost

> ModelApiResponse apiDemoReseedPost()



### Example

```ts
import {
  Configuration,
  DemoApi,
} from '';
import type { ApiDemoReseedPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new DemoApi();

  try {
    const data = await api.apiDemoReseedPost();
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

[**ModelApiResponse**](ModelApiResponse.md)

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


## apiDemoScene5Post

> ApiResponseOfMissionStateDto apiDemoScene5Post()



### Example

```ts
import {
  Configuration,
  DemoApi,
} from '';
import type { ApiDemoScene5PostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new DemoApi();

  try {
    const data = await api.apiDemoScene5Post();
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

