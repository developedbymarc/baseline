# ConfigApi

All URIs are relative to *http://localhost:5074*

| Method | HTTP request | Description |
|------------- | ------------- | -------------|
| [**apiConfigHowtoMetricCodeGet**](ConfigApi.md#apiconfighowtometriccodeget) | **GET** /api/config/howto/{metricCode} |  |
| [**apiConfigMetricsGet**](ConfigApi.md#apiconfigmetricsget) | **GET** /api/config/metrics |  |
| [**apiConfigProtocolsGet**](ConfigApi.md#apiconfigprotocolsget) | **GET** /api/config/protocols |  |
| [**apiConfigProtocolsIdGet**](ConfigApi.md#apiconfigprotocolsidget) | **GET** /api/config/protocols/{id} |  |



## apiConfigHowtoMetricCodeGet

> ApiResponseOfHowTo apiConfigHowtoMetricCodeGet(metricCode)



### Example

```ts
import {
  Configuration,
  ConfigApi,
} from '';
import type { ApiConfigHowtoMetricCodeGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new ConfigApi();

  const body = {
    // string
    metricCode: metricCode_example,
  } satisfies ApiConfigHowtoMetricCodeGetRequest;

  try {
    const data = await api.apiConfigHowtoMetricCodeGet(body);
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
| **metricCode** | `string` |  | [Defaults to `undefined`] |

### Return type

[**ApiResponseOfHowTo**](ApiResponseOfHowTo.md)

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


## apiConfigMetricsGet

> ApiResponseOfIEnumerableOfHealthMetric apiConfigMetricsGet()



### Example

```ts
import {
  Configuration,
  ConfigApi,
} from '';
import type { ApiConfigMetricsGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new ConfigApi();

  try {
    const data = await api.apiConfigMetricsGet();
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

[**ApiResponseOfIEnumerableOfHealthMetric**](ApiResponseOfIEnumerableOfHealthMetric.md)

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


## apiConfigProtocolsGet

> ApiResponseOfIEnumerableOfObject apiConfigProtocolsGet()



### Example

```ts
import {
  Configuration,
  ConfigApi,
} from '';
import type { ApiConfigProtocolsGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new ConfigApi();

  try {
    const data = await api.apiConfigProtocolsGet();
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


## apiConfigProtocolsIdGet

> ApiResponseOfProtocolDefinition apiConfigProtocolsIdGet(id)



### Example

```ts
import {
  Configuration,
  ConfigApi,
} from '';
import type { ApiConfigProtocolsIdGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new ConfigApi();

  const body = {
    // string
    id: id_example,
  } satisfies ApiConfigProtocolsIdGetRequest;

  try {
    const data = await api.apiConfigProtocolsIdGet(body);
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
| **id** | `string` |  | [Defaults to `undefined`] |

### Return type

[**ApiResponseOfProtocolDefinition**](ApiResponseOfProtocolDefinition.md)

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

