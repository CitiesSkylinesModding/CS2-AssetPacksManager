import { ModRegistrar } from 'cs2/modding';
import { cooperativePreloading } from '@csmodding/urbandevkit';

const register: ModRegistrar = moduleRegistry => {
    cooperativePreloading.register(moduleRegistry);
};

export default register;
