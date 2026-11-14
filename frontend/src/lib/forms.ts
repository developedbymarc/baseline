import * as yup from "yup";

export type ILoginFormData = yup.InferType<typeof LoginSchema>;
export const LoginSchema = yup.object({
  username: yup.string().min(2).required(),
  password: yup.string().min(3).required()
})

export type IMeasurementEntryFormData = yup.InferType<typeof MeasurementEntrySchema>;
export const MeasurementEntrySchema = yup.object({
  hrRest: yup.number().required(),
  bpSys: yup.number().required(),
  bpDia: yup.number().required(),
  temp: yup.number().required(),
  spo2: yup.number().required(),
  sleepH: yup.number().required()
})
