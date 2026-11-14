import { type ILoginFormData, LoginSchema } from '#lib/forms.js';
import { superValidate } from 'sveltekit-superforms';
import { message } from 'sveltekit-superforms';
import { fail } from '@sveltejs/kit';
import { yup } from 'sveltekit-superforms/adapters';
import { mockData } from '#lib/mockData.js';

export const load = async () => {
  const form = await superValidate<ILoginFormData>(yup(LoginSchema));
  return { form };
};


export const actions = {
  default: async ({ request }) => {
    const form = await superValidate(request, yup(LoginSchema));

    console.log(form)
    if (!form.valid) {
      return fail(400, { form });
    }

    if(form.data.username === mockData.user.username && form.data.password === mockData.user.password)
      return message(form, "Successfuly logged in");
    return message(form, 'Invalid username or password.', { status: 400 });
  }
};
