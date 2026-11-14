# ReportsApi

All URIs are relative to *http://localhost:5074*

| Method | HTTP request | Description |
|------------- | ------------- | -------------|
| [**apiCrewCrewIdReportsWindowPeriodGet**](ReportsApi.md#apicrewcrewidreportswindowperiodget) | **GET** /api/crew/{crewId}/reports/{window}/{period} |  |
| [**apiReportsCrewWindowPeriodGet**](ReportsApi.md#apireportscrewwindowperiodget) | **GET** /api/reports/crew/{window}/{period} |  |
| [**apiReportsPeriodsGet**](ReportsApi.md#apireportsperiodsget) | **GET** /api/reports/periods |  |



## apiCrewCrewIdReportsWindowPeriodGet

> ApiResponseOfReportDto apiCrewCrewIdReportsWindowPeriodGet(crewId, window, period)



### Example

```ts
import {
  Configuration,
  ReportsApi,
} from '';
import type { ApiCrewCrewIdReportsWindowPeriodGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new ReportsApi();

  const body = {
    // string
    crewId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    window: window_example,
    // string
    period: period_example,
  } satisfies ApiCrewCrewIdReportsWindowPeriodGetRequest;

  try {
    const data = await api.apiCrewCrewIdReportsWindowPeriodGet(body);
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
| **window** | `string` |  | [Defaults to `undefined`] |
| **period** | `string` |  | [Defaults to `undefined`] |

### Return type

[**ApiResponseOfReportDto**](ApiResponseOfReportDto.md)

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


## apiReportsCrewWindowPeriodGet

> ApiResponseOfReportDto apiReportsCrewWindowPeriodGet(window, period)



### Example

```ts
import {
  Configuration,
  ReportsApi,
} from '';
import type { ApiReportsCrewWindowPeriodGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new ReportsApi();

  const body = {
    // string
    window: window_example,
    // string
    period: period_example,
  } satisfies ApiReportsCrewWindowPeriodGetRequest;

  try {
    const data = await api.apiReportsCrewWindowPeriodGet(body);
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
| **window** | `string` |  | [Defaults to `undefined`] |
| **period** | `string` |  | [Defaults to `undefined`] |

### Return type

[**ApiResponseOfReportDto**](ApiResponseOfReportDto.md)

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


## apiReportsPeriodsGet

> ApiResponseOfIEnumerableOfReportPeriod apiReportsPeriodsGet(window)



### Example

```ts
import {
  Configuration,
  ReportsApi,
} from '';
import type { ApiReportsPeriodsGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new ReportsApi();

  const body = {
    // string (optional)
    window: window_example,
  } satisfies ApiReportsPeriodsGetRequest;

  try {
    const data = await api.apiReportsPeriodsGet(body);
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
| **window** | `string` |  | [Optional] [Defaults to `&#39;biweekly&#39;`] |

### Return type

[**ApiResponseOfIEnumerableOfReportPeriod**](ApiResponseOfIEnumerableOfReportPeriod.md)

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

