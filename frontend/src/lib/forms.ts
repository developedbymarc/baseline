import * as yup from "yup";

export type ILoginFormData = yup.InferType<typeof LoginSchema>;
export const LoginSchema = yup.object({
  username: yup.string().min(2).required(),
  password: yup.string().min(8).required()
})
