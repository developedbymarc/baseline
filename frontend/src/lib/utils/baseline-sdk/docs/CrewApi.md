# CrewApi

All URIs are relative to *http://localhost:5074*

| Method | HTTP request | Description |
|------------- | ------------- | -------------|
| [**apiCrewCrewIdAlertsGet**](CrewApi.md#apicrewcrewidalertsget) | **GET** /api/crew/{crewId}/alerts |  |
| [**apiCrewCrewIdBaselinesGet**](CrewApi.md#apicrewcrewidbaselinesget) | **GET** /api/crew/{crewId}/baselines |  |
| [**apiCrewCrewIdMeasurementsGet**](CrewApi.md#apicrewcrewidmeasurementsget) | **GET** /api/crew/{crewId}/measurements |  |
| [**apiCrewCrewIdMeasurementsPost**](CrewApi.md#apicrewcrewidmeasurementspost) | **POST** /api/crew/{crewId}/measurements |  |
| [**apiCrewCrewIdPacketsDraftGet**](CrewApi.md#apicrewcrewidpacketsdraftget) | **GET** /api/crew/{crewId}/packets/draft |  |
| [**apiCrewCrewIdPacketsPost**](CrewApi.md#apicrewcrewidpacketspost) | **POST** /api/crew/{crewId}/packets |  |
| [**apiCrewCrewIdProtocolRunsPost**](CrewApi.md#apicrewcrewidprotocolrunspost) | **POST** /api/crew/{crewId}/protocol-runs |  |
| [**apiCrewCrewIdRemindersGet**](CrewApi.md#apicrewcrewidremindersget) | **GET** /api/crew/{crewId}/reminders |  |
| [**apiCrewCrewIdSymptomsPost**](CrewApi.md#apicrewcrewidsymptomspost) | **POST** /api/crew/{crewId}/symptoms |  |
| [**apiCrewGet**](CrewApi.md#apicrewget) | **GET** /api/crew |  |
| [**apiCrewProtocolRunsRunIdEndPost**](CrewApi.md#apicrewprotocolrunsrunidendpost) | **POST** /api/crew/protocol-runs/{runId}/end |  |
| [**apiCrewProtocolRunsRunIdStepsPost**](CrewApi.md#apicrewprotocolrunsrunidstepspost) | **POST** /api/crew/protocol-runs/{runId}/steps |  |



## apiCrewCrewIdAlertsGet

> ApiResponseOfIEnumerableOfAlertDto apiCrewCrewIdAlertsGet(crewId)



### Example

```ts
import {
  Configuration,
  CrewApi,
} from '';
import type { ApiCrewCrewIdAlertsGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new CrewApi();

  const body = {
    // string
    crewId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies ApiCrewCrewIdAlertsGetRequest;

  try {
    const data = await api.apiCrewCrewIdAlertsGet(body);
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
| **crewId** | `string` |  | [Defaults to `undefined`] |

### Return type

[**ApiResponseOfIEnumerableOfAlertDto**](ApiResponseOfIEnumerableOfAlertDto.md)

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


## apiCrewCrewIdBaselinesGet

> ApiResponseOfIEnumerableOfObject apiCrewCrewIdBaselinesGet(crewId)



### Example

```ts
import {
  Configuration,
  CrewApi,
} from '';
import type { ApiCrewCrewIdBaselinesGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new CrewApi();

  const body = {
    // string
    crewId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies ApiCrewCrewIdBaselinesGetRequest;

  try {
    const data = await api.apiCrewCrewIdBaselinesGet(body);
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
| **crewId** | `string` |  | [Defaults to `undefined`] |

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


## apiCrewCrewIdMeasurementsGet

> ApiResponseOfIEnumerableOfObject apiCrewCrewIdMeasurementsGet(crewId, metric, days)



### Example

```ts
import {
  Configuration,
  CrewApi,
} from '';
import type { ApiCrewCrewIdMeasurementsGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new CrewApi();

  const body = {
    // string
    crewId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string (optional)
    metric: metric_example,
    // ApiCrewCrewIdMeasurementsGetDaysParameter (optional)
    days: 56,
  } satisfies ApiCrewCrewIdMeasurementsGetRequest;

  try {
    const data = await api.apiCrewCrewIdMeasurementsGet(body);
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
| **crewId** | `string` |  | [Defaults to `undefined`] |
| **metric** | `string` |  | [Optional] [Defaults to `undefined`] |
| **days** | [](.md) |  | [Optional] [Defaults to `undefined`] |

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


## apiCrewCrewIdMeasurementsPost

> apiCrewCrewIdMeasurementsPost(crewId, submitMeasurementRequest)



### Example

```ts
import {
  Configuration,
  CrewApi,
} from '';
import type { ApiCrewCrewIdMeasurementsPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new CrewApi();

  const body = {
    // string
    crewId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // SubmitMeasurementRequest
    submitMeasurementRequest: ...,
  } satisfies ApiCrewCrewIdMeasurementsPostRequest;

  try {
    const data = await api.apiCrewCrewIdMeasurementsPost(body);
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
| **crewId** | `string` |  | [Defaults to `undefined`] |
| **submitMeasurementRequest** | [SubmitMeasurementRequest](SubmitMeasurementRequest.md) |  | |

### Return type

`void` (Empty response body)

### Authorization

No authorization required

### HTTP request headers

- **Content-Type**: `application/json`, `text/json`, `application/*+json`
- **Accept**: Not defined


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** | OK |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


## apiCrewCrewIdPacketsDraftGet

> ApiResponseOfHandoffPacket apiCrewCrewIdPacketsDraftGet(crewId, alertId, runId)



### Example

```ts
import {
  Configuration,
  CrewApi,
} from '';
import type { ApiCrewCrewIdPacketsDraftGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new CrewApi();

  const body = {
    // string
    crewId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string (optional)
    alertId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string (optional)
    runId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies ApiCrewCrewIdPacketsDraftGetRequest;

  try {
    const data = await api.apiCrewCrewIdPacketsDraftGet(body);
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
| **crewId** | `string` |  | [Defaults to `undefined`] |
| **alertId** | `string` |  | [Optional] [Defaults to `undefined`] |
| **runId** | `string` |  | [Optional] [Defaults to `undefined`] |

### Return type

[**ApiResponseOfHandoffPacket**](ApiResponseOfHandoffPacket.md)

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


## apiCrewCrewIdPacketsPost

> ApiResponseOfLinkMessageDto apiCrewCrewIdPacketsPost(crewId, queuePacketRequest)



### Example

```ts
import {
  Configuration,
  CrewApi,
} from '';
import type { ApiCrewCrewIdPacketsPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new CrewApi();

  const body = {
    // string
    crewId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // QueuePacketRequest
    queuePacketRequest: ...,
  } satisfies ApiCrewCrewIdPacketsPostRequest;

  try {
    const data = await api.apiCrewCrewIdPacketsPost(body);
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
| **crewId** | `string` |  | [Defaults to `undefined`] |
| **queuePacketRequest** | [QueuePacketRequest](QueuePacketRequest.md) |  | |

### Return type

[**ApiResponseOfLinkMessageDto**](ApiResponseOfLinkMessageDto.md)

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


## apiCrewCrewIdProtocolRunsPost

> ApiResponseOfProtocolRunDto apiCrewCrewIdProtocolRunsPost(crewId, startProtocolRequest)



### Example

```ts
import {
  Configuration,
  CrewApi,
} from '';
import type { ApiCrewCrewIdProtocolRunsPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new CrewApi();

  const body = {
    // string
    crewId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // StartProtocolRequest
    startProtocolRequest: ...,
  } satisfies ApiCrewCrewIdProtocolRunsPostRequest;

  try {
    const data = await api.apiCrewCrewIdProtocolRunsPost(body);
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
| **crewId** | `string` |  | [Defaults to `undefined`] |
| **startProtocolRequest** | [StartProtocolRequest](StartProtocolRequest.md) |  | |

### Return type

[**ApiResponseOfProtocolRunDto**](ApiResponseOfProtocolRunDto.md)

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


## apiCrewCrewIdRemindersGet

> ApiResponseOfIEnumerableOfObject apiCrewCrewIdRemindersGet(crewId, openOnly)



### Example

```ts
import {
  Configuration,
  CrewApi,
} from '';
import type { ApiCrewCrewIdRemindersGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new CrewApi();

  const body = {
    // string
    crewId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // boolean (optional)
    openOnly: true,
  } satisfies ApiCrewCrewIdRemindersGetRequest;

  try {
    const data = await api.apiCrewCrewIdRemindersGet(body);
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
| **crewId** | `string` |  | [Defaults to `undefined`] |
| **openOnly** | `boolean` |  | [Optional] [Defaults to `true`] |

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


## apiCrewCrewIdSymptomsPost

> ApiResponseOfObject apiCrewCrewIdSymptomsPost(crewId, logSymptomRequest)



### Example

```ts
import {
  Configuration,
  CrewApi,
} from '';
import type { ApiCrewCrewIdSymptomsPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new CrewApi();

  const body = {
    // string
    crewId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // LogSymptomRequest
    logSymptomRequest: ...,
  } satisfies ApiCrewCrewIdSymptomsPostRequest;

  try {
    const data = await api.apiCrewCrewIdSymptomsPost(body);
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
| **crewId** | `string` |  | [Defaults to `undefined`] |
| **logSymptomRequest** | [LogSymptomRequest](LogSymptomRequest.md) |  | |

### Return type

[**ApiResponseOfObject**](ApiResponseOfObject.md)

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


## apiCrewGet

> ApiResponseOfIEnumerableOfCrewMemberDto apiCrewGet()



### Example

```ts
import {
  Configuration,
  CrewApi,
} from '';
import type { ApiCrewGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new CrewApi();

  try {
    const data = await api.apiCrewGet();
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

[**ApiResponseOfIEnumerableOfCrewMemberDto**](ApiResponseOfIEnumerableOfCrewMemberDto.md)

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


## apiCrewProtocolRunsRunIdEndPost

> ApiResponseOfProtocolRunDto apiCrewProtocolRunsRunIdEndPost(runId, endProtocolRequest)



### Example

```ts
import {
  Configuration,
  CrewApi,
} from '';
import type { ApiCrewProtocolRunsRunIdEndPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new CrewApi();

  const body = {
    // string
    runId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // EndProtocolRequest
    endProtocolRequest: ...,
  } satisfies ApiCrewProtocolRunsRunIdEndPostRequest;

  try {
    const data = await api.apiCrewProtocolRunsRunIdEndPost(body);
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
| **runId** | `string` |  | [Defaults to `undefined`] |
| **endProtocolRequest** | [EndProtocolRequest](EndProtocolRequest.md) |  | |

### Return type

[**ApiResponseOfProtocolRunDto**](ApiResponseOfProtocolRunDto.md)

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


## apiCrewProtocolRunsRunIdStepsPost

> ApiResponseOfProtocolRunDto apiCrewProtocolRunsRunIdStepsPost(runId, protocolStepRequest)



### Example

```ts
import {
  Configuration,
  CrewApi,
} from '';
import type { ApiCrewProtocolRunsRunIdStepsPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new CrewApi();

  const body = {
    // string
    runId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // ProtocolStepRequest
    protocolStepRequest: ...,
  } satisfies ApiCrewProtocolRunsRunIdStepsPostRequest;

  try {
    const data = await api.apiCrewProtocolRunsRunIdStepsPost(body);
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
| **runId** | `string` |  | [Defaults to `undefined`] |
| **protocolStepRequest** | [ProtocolStepRequest](ProtocolStepRequest.md) |  | |

### Return type

[**ApiResponseOfProtocolRunDto**](ApiResponseOfProtocolRunDto.md)

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

