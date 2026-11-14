import { writable } from 'svelte/store';
import { mockData } from './mockData';

export const userStore = writable({
  isLogged: false,
  ...mockData.user
})
